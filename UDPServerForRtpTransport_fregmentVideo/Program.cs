using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using UDPServerTest.Decode;

namespace UDPTest
{
    class Program
    {
        static void Main(string[] args)
        {

            #region udpServer

            //建立UDPSocket 参数2：udp协议以数据报的方式传输，参数3：UDP协议
            Socket udpServer = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

            //为udp服务器绑定ip
            IPAddress ip = IPAddress.Parse(getLocalIpAddress(3));
            EndPoint ipAddress = new IPEndPoint(ip, 18888);
            udpServer.Bind(ipAddress);

            //接收数据 本机的所有IP地址，所有可用的端口
            EndPoint clientAddress = new IPEndPoint(IPAddress.Any, 0);

            #endregion

            #region  缓存区和索引
            //单个数据报缓存区
            byte[] data = new byte[1450];
            //H264缓存区
            byte[] H264Buffer = new byte[5*1024 * 1024];

            //分片NALU缓存区
            byte[] fregmentBuffer = new byte[10 * 1024 * 1024];

            //SPS+PPS+SEI缓存区
            byte[] sps_pps_seiBuffer = new byte[1024 * 1024];

            //建立每一个I帧P帧视频序列缓存区
            byte[] iFrameAndpFrameSequenceBuffer = new byte[10 * 1024 * 1024];

            //获取到的H.264插入视频缓存区中的位置
            int index = 0;

            //SPS+PPS+SEI缓存区中的索引位置
            int spsIndex = 0;

            //每一个I帧P帧视频序列缓存区中索引的位置
            int videoSequenceIndex = 0;

            #endregion


            //收到的RTP Package的length
            int length = 0;

            //需要分片的NALU的原始的长度
            int fregmentNaluLength = 0;

            //IDR帧计算
            int iDRFrameCount = 0;

            //int spsFrameLength = 0;
            //int ppsFrameLength = 0;


            //设置分片NALU原始H264数据的起始码
            byte[] startCode = new byte[4];
            startCode[0] = 0x00;
            startCode[1] = 0x00;
            startCode[2] = 0x00;
            startCode[3] = 0x01;

            //设置分片NALU原始H264的NALU Header
            int fuIndicatorTop3Bit = 0;
            int fuHeaderLast5Bit = 0;
            //int naluHeaderIndex = 0;
            //int rtpCount = 0;

            int fregmentFileCount = 0;

            Console.WriteLine("服务端已开启，监听端口43999");

            //把数据的来源放到第二个参数上
            while (true)
            {
                //收到每一个数据报 
                length = udpServer.ReceiveFrom(data, ref clientAddress);

                byte[] sequenceNumberBytes = new byte[4];
                sequenceNumberBytes[0] = data[3];
                sequenceNumberBytes[1] = data[2];
                int sequenceNumber = BitConverter.ToInt32(sequenceNumberBytes, 0);
                //Console.SetCursorPosition(0, 1);
                Console.WriteLine(sequenceNumber);

                if (length != 1)
                {
                    if (sequenceNumber == 3)
                    {
                        //首先将SPS+PPS+SEI缓存区中的数据存入每一个H264Buffer最前面
                        DecodeRTPHelper.CopyToH264(H264Buffer, sps_pps_seiBuffer, spsIndex, ref index);
                        index += spsIndex;
                    }
                    //判断收到的为单一NALU模式
                    if ((data[12] & 0x1f) != 28)
                    {
                        //首先将SPS_PPS_SEI存入SPS+PPS+SEI缓存区中
                        if (sequenceNumber<=2)
                        {
                            var sps_pps_seiBytes = DecodeRTPHelper.DecodeSingleNalu(data, length, ref spsIndex, ref iDRFrameCount);
                            Array.Copy(sps_pps_seiBytes, 0, sps_pps_seiBuffer, spsIndex, sps_pps_seiBytes.Length);
                            spsIndex += sps_pps_seiBytes.Length;
                            continue;
                        }

                        //获取原始的H.264数据，包含了起始字节00 00 00 01
                        var singleNalu = DecodeRTPHelper.DecodeSingleNalu(data, length, ref videoSequenceIndex, ref iDRFrameCount);

                        //此时到下一个IDR帧了
                        if (iDRFrameCount == 2)
                        {
                            //加上一个序列之后，长度超出H264Buffer缓存区的长度
                            if (index + videoSequenceIndex > H264Buffer.Length)
                            {
                                //先将之前的输出到文件
                                Interlocked.Increment(ref fregmentFileCount);
                                FileStream sourceFileStream = new FileStream(@"D:\vsProject\VS2019Project\FileStorage\Test" +
                                    Convert.ToInt32(DateTime.Now.TimeOfDay.TotalMilliseconds) + ".264", FileMode.Create);
                                using (sourceFileStream)
                                {
                                    sourceFileStream.Write(H264Buffer, 0, index);
                                    Console.WriteLine("写入文件Test{0}.264,此时结束原因是1：index + videoSequenceIndex > H264Buffer.Length.", fregmentFileCount);
                                }

                                //将之前存好的序列存入H264Buffer中
                                index = spsIndex;
                                DecodeRTPHelper.CopyToH264(H264Buffer, iFrameAndpFrameSequenceBuffer, videoSequenceIndex, ref index);
                                index += videoSequenceIndex;
                                videoSequenceIndex = 0;
                                

                                //将当前收到的单一NALU存入iFrameAndpFrameSequenceBuffer
                                Array.Copy(singleNalu, 0, iFrameAndpFrameSequenceBuffer, videoSequenceIndex, singleNalu.Length);
                                videoSequenceIndex += singleNalu.Length;
                                iDRFrameCount = 1;
                            }
                            else
                            {
                                //将IFrameBuffer存入H264Buffer中
                                DecodeRTPHelper.CopyToH264(H264Buffer, iFrameAndpFrameSequenceBuffer, videoSequenceIndex, ref index);
                                index += videoSequenceIndex;
                                videoSequenceIndex = 0;
                                

                                Array.Copy(singleNalu, 0, iFrameAndpFrameSequenceBuffer, videoSequenceIndex, singleNalu.Length);
                                videoSequenceIndex += singleNalu.Length;
                                iDRFrameCount = 1;

                            }

                        }
                        else
                        {
                            //将原始的单个H.264数据存入iFrameAndpFrameSequenceBuffer中
                            Array.Copy(singleNalu, 0, iFrameAndpFrameSequenceBuffer, videoSequenceIndex, singleNalu.Length);
                            videoSequenceIndex += singleNalu.Length;
                        }
                    }
                    //收到的为分片NALU模式
                    else
                    {
                        //如果为最后分片
                        if ((data[13] & 0xc0) == 64)
                        {
                            //将所有分片NALU的片段存入fregmentBuffer中
                            DecodeRTPHelper.DecodeFregmentNalu(data, startCode, fregmentBuffer, length,
                                ref fregmentNaluLength, ref fuIndicatorTop3Bit, ref fuHeaderLast5Bit,ref iDRFrameCount,ref videoSequenceIndex);

                            //如果检测到收到的分片NALU为IDR帧
                            if (iDRFrameCount == 2)
                            {

                                //如果之前已存的视频数据+新存入的iFrameAndpFrameSequenceBuffer的总长度超过H264Buffer的总长度
                                if (index + videoSequenceIndex > H264Buffer.Length)
                                {
                                    //将之前的H264Buffer中的数据先存入文件
                                    Interlocked.Increment(ref fregmentFileCount);
                                    FileStream sourceFileStream = new FileStream(@"D:\vsProject\VS2019Project\FileStorage\Test" + 
                                        Convert.ToInt32(DateTime.Now.TimeOfDay.TotalMilliseconds) + ".264", FileMode.Create);
                                    using (sourceFileStream)
                                    {
                                        sourceFileStream.Write(H264Buffer, 0, index);
                                        Console.WriteLine("写入文件Test{0}.264,此时结束原因是2：index + videoSequenceIndex > H264Buffer.Length.", fregmentFileCount);
                                    }

                                    //将之前存好的iFrameAndpFrameSequenceBuffer存入H264Buffer中
                                    index = spsIndex;
                                    DecodeRTPHelper.CopyToH264(H264Buffer, iFrameAndpFrameSequenceBuffer, videoSequenceIndex, ref index);
                                    index += videoSequenceIndex;
                                    videoSequenceIndex = 0;
                                    

                                    //将此时收到fregmentBuffer存入iFrameAndpFrameSequenceBuffer中
                                    Array.Copy(fregmentBuffer, 0, iFrameAndpFrameSequenceBuffer, videoSequenceIndex, fregmentNaluLength);
                                    videoSequenceIndex += fregmentNaluLength;
                                    fregmentNaluLength = 0;
                                    iDRFrameCount = 1;


                                }
                                //如果之前的数据加上刚收的分片NALU的长度没超过H264Buffer的长度
                                else
                                {
                                    DecodeRTPHelper.CopyToH264(H264Buffer, iFrameAndpFrameSequenceBuffer, videoSequenceIndex, ref index);
                                    index += videoSequenceIndex;
                                    videoSequenceIndex = 0;
                                    

                                    //将此时收到fregmentBuffer存入iFrameAndpFrameSequenceBuffer中
                                    Array.Copy(fregmentBuffer, 0, iFrameAndpFrameSequenceBuffer, videoSequenceIndex, fregmentNaluLength);
                                    videoSequenceIndex += fregmentNaluLength;
                                    fregmentNaluLength = 0;
                                    iDRFrameCount = 1;


                                }
                            }
                            else
                            {
                                //将完整的fregmentBuffer存入iFrameAndpFrameSequenceBuffer中
                                Array.Copy(fregmentBuffer, 0, iFrameAndpFrameSequenceBuffer, videoSequenceIndex, fregmentNaluLength);
                                videoSequenceIndex += fregmentNaluLength;
                                fregmentNaluLength = 0;
                            }

                        }
                        //如果不为最后分片
                        else
                        {
                            //将所有分片NALU的片段存入分片NALU缓存区中
                            DecodeRTPHelper.DecodeFregmentNalu(data, startCode, fregmentBuffer, length,
                                ref fregmentNaluLength, ref fuIndicatorTop3Bit, ref fuHeaderLast5Bit,ref iDRFrameCount,ref videoSequenceIndex);
                        }

                    }
                }
                else
                {
                    Interlocked.Increment(ref fregmentFileCount);
                    FileStream fs = new FileStream(@"D:\vsProject\VS2019Project\FileStorage\Test" + 
                        Convert.ToInt32(DateTime.Now.TimeOfDay.TotalMilliseconds) + ".264", FileMode.Create);
                    using (fs)
                    {
                        fs.Write(H264Buffer, 0, index);
                        Console.WriteLine("写入文件Test{0}.264,此时结束原因是3：rap pacakge length ==1.", fregmentFileCount);
                        //fs.Close();
                    }
                    break;
                }
            }

            Console.WriteLine("视频流写入文件完成.");
            ////将读取到的视频缓存区中的数据存入文件中
            //FileStream fileStream = new FileStream(@"D:\vsProject\VS2019Project\FileStorage\Test" + Convert.ToInt32(DateTime.Now.TimeOfDay.TotalMilliseconds) + ".264",
            //    FileMode.Create);

            //using (fileStream)
            //{
            //    fileStream.Write(H264Buffer, 0, index);
            //    Console.WriteLine("获取到的视频流写入文件完成");
            //    fileStream.Close();
            //}
        }

        /// <summary>
        /// 获取本机IP地址，3：外网IPV4地址，5：局域网IPV4地址
        /// </summary>
        /// <param name="addressNumber"></param>
        /// <returns></returns>
        private static string getLocalIpAddress(int addressNumber)
        {
            //获得本机局域网IP
            IPAddress[] addressList = Dns.GetHostEntry(Dns.GetHostName()).AddressList;

            if (addressList.Length < 1)
            {
                return "";
            }

            //5是外网IPV4
            //6是以太网IPV4
            return addressList[addressNumber].ToString();
        }
    }
}


