using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using UDPClientTest.RTPProtocol;

namespace UDPClientTest
{
    class Program
    {
        static void Main(string[] args)
        {
            //建立UDPClientSocket,参数2：udp协议以数据报的方式传输，参数3：UDP协议
            Socket udpClient = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

            IPAddress ip = IPAddress.Parse(GetLocalAddress(5));

            EndPoint serverIPAddress = new IPEndPoint(ip, 43999);

            Console.WriteLine("开始基于UDP传输RTP数据报....");

            //先获取H.264视频流字节组
            var sourceH264Bytes = ConvertFileToByte(@"E:\研二上学期\项目\VideoTransport\裸码流\normalStream.264");

            int index = 0;
            int sequenceNumber = 0;
            //输入客户端需要发送给服务端的信息
            while (true)
            {
                //获取到其中的一个NALU
                var oneNaluBytes = ProtocolHelper.GetPerNALU(sourceH264Bytes, ref index, out int startCodeLength);
                //从新给索引赋值以便下一次遍历
                index += oneNaluBytes.Length + startCodeLength;

                //若NALU的长度超过MTU，采用分片封包模式
                if (oneNaluBytes.Length > 1400)
                {
                    //分片个数
                    int fregmentCount = oneNaluBytes.Length % 1400 != 0 ? oneNaluBytes.Length / 1400 + 1 : oneNaluBytes.Length / 1400;

                    for(int i = 1; i <= fregmentCount; i++)
                    {
                        //分片nalu长度
                        int fregmentNaluLength;
                        if (i < fregmentCount)
                        {
                            fregmentNaluLength = 1400;
                        }
                        //最后一个分片长度
                        else
                        {
                            fregmentNaluLength = oneNaluBytes.Length % (1400 * (i - 1));
                        }

                        //获取分片RTP Package
                        var fregmentRtpPackageBytes = ProtocolHelper.FragmentUnit(oneNaluBytes, fregmentNaluLength, fregmentCount, i, ref sequenceNumber);
                        udpClient.SendTo(fregmentRtpPackageBytes, serverIPAddress);
                    }
                }
                //单一NALU模式
                else
                {
                    //获取单一Nalu模式的RTP Package
                    var singleNaluBytes = ProtocolHelper.RtpPackage(oneNaluBytes, ref sequenceNumber);
                    udpClient.SendTo(singleNaluBytes, serverIPAddress);
                }

                //若发送完整个H264
                if (index == sourceH264Bytes.Length)
                {
                    break;
                }
            }

            byte[] endFlag = { 0x11 };
            udpClient.SendTo(endFlag, serverIPAddress);

            Console.WriteLine("H264数据已经通过打包为RTP Package并基于UDP发送到服务器端.");

        }


        /// <summary>
        /// 读取给定的文件，将文件转换为数据包bytes
        /// </summary>
        /// <param name="filePath">文件绝对路径</param>
        /// <returns>文件的字节数据包</returns>
        static byte[] ConvertFileToByte(string filePath)
        {
            //判断给定的文件路径是否存在
            if (!File.Exists(filePath))
            {
                return null;
            }

            //新建文件流，模式为打开文件
            FileStream fs = new FileStream(filePath, FileMode.Open);

            //新建文件缓存区
            byte[] fileBytes = new byte[fs.Length];

            using (fs)
            {
                //从文件流中将字节数据读入到缓冲区
                fs.Read(fileBytes, 0, fileBytes.Length);
                fs.Close();
            }

            return fileBytes;
        }


        /// <summary>
        /// 获取本机IP地址    3：本机外网IPV4;  5：本机局域网IPV4
        /// </summary>
        /// <param name="addressNumber">需要获取的本机的某种ip在IPAddress[]中的编号</param>
        /// <returns></returns>
        private static string GetLocalAddress(int addressNumber)
        {
            IPAddress[] addressList = Dns.GetHostEntry(Dns.GetHostName()).AddressList;

            if (addressList.Length < 1)
            {
                return "";
            }
            return addressList[addressNumber].ToString();
        }
    }
}
