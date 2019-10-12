using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
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
            IPAddress ip = IPAddress.Parse(getLocalIpAddress(5));
            EndPoint ipAddress = new IPEndPoint(ip, 43999);
            udpServer.Bind(ipAddress);

            //接收数据 本机的所有IP地址，所有可用的端口
            EndPoint clientAddress = new IPEndPoint(IPAddress.Any, 0);
            //单个数据报缓存区
            byte[] data = new byte[1450];
            //H264缓存区
            byte[] H264Buffer = new byte[20 * 1024 * 1024];
            int length = 0;
            //获取到的H.264插入视频缓存区中的位置
            int index = 0;

            //设置分片NALU原始H264数据的起始码
            byte[] startCode = new byte[4];
            startCode[0] = 0x00;
            startCode[1] = 0x00;
            startCode[2] = 0x00;
            startCode[3] = 0x01;

            //设置分片NALU原始H264的NALU Header
            int fuIndicatorTop3Bit=0;
            int fuHeaderLast5Bit=0;
            int naluHeaderIndex=0;

            //把数据的来源放到第二个参数上-
            while (true)
            {
                //收到每一个数据报
                length = udpServer.ReceiveFrom(data, ref clientAddress);
                if (length !=1)
                {
                    //判断收到的为单一NALU模式
                    if ((data[12] & 0x1f) != 28)
                    {
                        //获取原始的H.264数据
                        var singleNalu = DecodeRTPHelper.DecodeSingleNalu(data,length, ref index, out int sequenceNumber);
                        //将原始的单个H.264数据存入视频缓存区中
                        Array.Copy(singleNalu, 0, H264Buffer, index, singleNalu.Length);
                        index += singleNalu.Length;

                    }
                    //收到的为分片NALU模式
                    else
                    {
                        DecodeRTPHelper.DecodeFregmentNalu(H264Buffer, data, startCode, length, ref index, ref naluHeaderIndex, ref fuIndicatorTop3Bit, ref fuHeaderLast5Bit);
                    }
                }
                else
                {
                    break;
                }
            }
            //将读取到的视频缓存区中的数据存入文件中
            FileStream fileStream = new FileStream(@"D:\vsProject\VS2019Project\FileStorage\Test" + Convert.ToInt32(DateTime.Now.TimeOfDay.TotalMilliseconds) + ".264",
                FileMode.Create);

            using (fileStream)
            {
                fileStream.Write(H264Buffer, 0, index);
                Console.WriteLine("获取到的视频流写入文件完成");
                fileStream.Close();
            }
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
