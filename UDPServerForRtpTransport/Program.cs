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
            //建立UDPSocket 参数2：udp协议以数据报的方式传输，参数3：UDP协议
            Socket udpServer = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

            //为udp服务器绑定ip
            IPAddress ip = IPAddress.Parse(getLocalIpAddress(3));
            EndPoint ipAddress = new IPEndPoint(ip, 43999);
            udpServer.Bind(ipAddress);

            //接收数据 本机的所有IP地址，所有可用的端口
            EndPoint clientAddress = new IPEndPoint(IPAddress.Any, 0);
            //单个数据报缓存区
            byte[] data = new byte[1450];
            //H264缓存区
            byte[] H264Buffer = new byte[1 * 1024 * 1024];
            //byte[] zeroBuffer = new byte[20 * 1024 * 1024];
            int length = 0;

            //分片NALU缓存区
            byte[] fregmentBuffer = new byte[10 * 1024 * 1024];

            //需要分片的NALU的原始的长度
            int fregmentNaluLength = 0;

            //获取到的H.264插入视频缓存区中的位置
            int index = 0;

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
            int rtpCount = 0;

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
                    #region   注释
                    //如果收到的数据帧总长度超过缓存区的长度
                    //if ((H264Buffer.Length - index) < 1400)
                    //{
                    //    Interlocked.Increment(ref fregmentFileCount);
                    //    FileStream sourceFileStream = new FileStream(@"D:\vsProject\VS2019Project\FileStorage\Test"+fregmentFileCount+".264", FileMode.Create);
                    //    using (sourceFileStream)
                    //    {
                    //        sourceFileStream.Write(H264Buffer, 0, index);
                    //        sourceFileStream.Close();
                    //    }
                    //    index = 0;
                    //}
                    //else
                    //{
                    #endregion
                    //判断收到的为单一NALU模式
                    if ((data[12] & 0x1f) != 28)
                    {
                        //获取原始的H.264数据，包含了起始字节00 00 00 01
                        var singleNalu = DecodeRTPHelper.DecodeSingleNalu(data, length, ref index);
                        //将原始的单个H.264数据存入视频缓存区中
                        Array.Copy(singleNalu, 0, H264Buffer, index, singleNalu.Length);
                        index += singleNalu.Length;
                        //如果收到的数据帧总长度超过缓存区的长度
                        if ((H264Buffer.Length - index) < 1420)
                        {
                            Interlocked.Increment(ref fregmentFileCount);
                            FileStream sourceFileStream = new FileStream(@"D:\vsProject\VS2019Project\FileStorage\Test" + fregmentFileCount + ".264", FileMode.Create);
                            using (sourceFileStream)
                            {
                                sourceFileStream.Write(H264Buffer, 0, index);
                                Console.WriteLine("写入文件Test{0}.264,此时结束原因是1：(H264Buffer.Length - index) < 1420.", fregmentFileCount);
                                //sourceFileStream.Close();
                            }
                            index = 0;
                        }

                    }
                    //收到的为分片NALU模式
                    else
                    {
                        //如果为最后分片
                        if ((data[13] & 0xc0) == 64)
                        {
                            //将所有分片NALU的片段存入分片NALU缓存区中
                            DecodeRTPHelper.DecodeFregmentNalu(data, startCode, fregmentBuffer, length,
                                ref fregmentNaluLength, ref fuIndicatorTop3Bit, ref fuHeaderLast5Bit);

                            //首先判断单一NALU存完之后视频流中的索引位置加上分片NALU的长度是否超过视频流缓存区的长度
                            if (index + fregmentNaluLength > H264Buffer.Length)
                            {
                                Interlocked.Increment(ref fregmentFileCount);
                                FileStream sourceFileStream = new FileStream(@"D:\vsProject\VS2019Project\FileStorage\Test" + fregmentFileCount + ".264", FileMode.Create);
                                using (sourceFileStream)
                                {
                                    sourceFileStream.Write(H264Buffer, 0, index);
                                    Console.WriteLine("写入文件Test{0}.264,此时结束原因是2：index + fregmentNaluLength > H264Buffer.Length.", fregmentFileCount);
                                    //sourceFileStream.Close();
                                }
                                index = 0;
                                Array.Copy(fregmentBuffer, 0, H264Buffer, index, fregmentNaluLength);
                                index += fregmentNaluLength;

                                fregmentNaluLength = 0;
                            }
                            //如果除去之前已经存的H264帧和刚收到的分片NALU之后，视频流缓存区的长度小于1400;
                            else if (H264Buffer.Length - index - fregmentNaluLength < 1420)
                            {
                                Array.Copy(fregmentBuffer, 0, H264Buffer, index, fregmentNaluLength);
                                index += fregmentNaluLength;
                                Interlocked.Increment(ref fregmentFileCount);
                                FileStream sourceFileStream = new FileStream(@"D:\vsProject\VS2019Project\FileStorage\Test" + fregmentFileCount + ".264", FileMode.Create);
                                using (sourceFileStream)
                                {
                                    sourceFileStream.Write(H264Buffer, 0, index);
                                    Console.WriteLine("写入文件Test{0}.264,此时结束原因是3：H264Buffer.Length - index - fregmentNaluLength < 1420.", fregmentFileCount);
                                    //sourceFileStream.Close();
                                }
                                index = 0;
                                fregmentNaluLength = 0;
                            }
                            //正常情况，分片NALU不会在视频流缓存区的最后一帧出现
                            else
                            {
                                Array.Copy(fregmentBuffer, 0, H264Buffer, index, fregmentNaluLength);
                                index += fregmentNaluLength;
                                fregmentNaluLength = 0;
                            }

                            //将分片NALU缓存区中的数据存入视频流缓存区中
                            

                        }
                        //如果不为最后分片
                        else
                        {
                            //将所有分片NALU的片段存入分片NALU缓存区中
                            DecodeRTPHelper.DecodeFregmentNalu(data, startCode, fregmentBuffer, length,
                                ref fregmentNaluLength, ref fuIndicatorTop3Bit, ref fuHeaderLast5Bit);
                        }

                    }
                    //}
                }
                else
                {
                    Interlocked.Increment(ref fregmentFileCount);
                    FileStream fs = new FileStream(@"D:\vsProject\VS2019Project\FileStorage\Test" + fregmentFileCount + ".264", FileMode.Create);
                    using (fs)
                    {
                        fs.Write(H264Buffer, 0, index);
                        Console.WriteLine("写入文件Test{0}.264,此时结束原因是4：rap pacakge length ==1.", fregmentFileCount);
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
