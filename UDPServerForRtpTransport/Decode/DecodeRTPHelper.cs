using System;
using System.Collections.Generic;
using System.Text;

namespace UDPServerTest.Decode
{
    public class DecodeRTPHelper
    {


        /// <summary>
        /// 对单一NALU的RTP包进行解包为原始的H264帧，同时输出当前Package的序列号
        /// </summary>
        /// <param name="sourceRtpPackage">获取到的单一NALU的RTP包</param>
        /// <param name="sequenceNumber">序列号</param>
        /// <returns>原始的H264帧</returns>
        public static byte[] DecodeSingleNalu(byte[] sourceRtpPackage,int length,ref int index,out int sequenceNumber)
        {
            //建立序列号缓存区，并将RTP Package中的序列号拷贝出来
            byte[] sequenceNumberBytes = new byte[4];
            sequenceNumberBytes[0] = sourceRtpPackage[3];
            sequenceNumberBytes[1] = sourceRtpPackage[2];
            sequenceNumber = BitConverter.ToInt32(sequenceNumberBytes, 0);

            //建立原始H264缓存区
            byte[] sourceH264Bytes = new byte[4 + length - 12];
            //首先设置H264起始码
            sourceH264Bytes[0] = 0x00;
            sourceH264Bytes[1] = 0x00;
            sourceH264Bytes[2] = 0x00;
            sourceH264Bytes[3] = 0x01;

            //合并起始码和NALU
            Array.Copy(sourceRtpPackage, 12, sourceH264Bytes, 4, length - 12);
            return sourceH264Bytes;
        }



        /// <summary>
        /// 对分片NALU的RTP包进行解包，并将其复制到视频流缓存区
        /// </summary>
        /// <param name="sourceH264Bbytes">视频流缓存区</param>
        /// <param name="sourceRtpPackage">通过UDP收到的RTP数据报</param>
        /// <param name="startCode">起始码字节</param>
        /// <param name="index">即将插入到视频流缓存区中的索引位置</param>
        /// <param name="naluHeaderIndex">分片模式中，naluHeader的索引位置</param>
        /// <param name="fuIndicatorTop3Bit">fu indecator的前三位</param>
        /// <param name="fuHeaderLast5Bit">fu header的后五位</param>
        /// <param name="naluHeader">由fu indecator的前三位和fu header的后五位组成的naluHeader</param>
        /// <returns>插入分片Nalu之后的视频流缓存区</returns>
        public static byte[] DecodeFregmentNalu(byte[] sourceH264Bbytes,byte[] sourceRtpPackage,byte[] startCode,int length,
            ref int index,ref int naluHeaderIndex,ref int fuIndicatorTop3Bit,ref int fuHeaderLast5Bit)
        {

            //建立序列号缓存区
            byte[] sequenceNumberBytes = new byte[2];
            sequenceNumberBytes[0] = sourceRtpPackage[3];
            sequenceNumberBytes[1] = sourceRtpPackage[2];
            //sequenceNumber = BitConverter.ToInt32(sequenceNumberBytes, 0);

            //判断该分片为起始，中间还是结尾分片
            switch (sourceRtpPackage[13] & 0xc0)
            {
                //第一分片
                case 128:
                    //先确定naluHeader的索引位置
                    naluHeaderIndex = index + 4;
                    //获取FU indicator的前三位F_NRI
                    fuIndicatorTop3Bit = 0xe0 & sourceRtpPackage[12];
                    //将startCode存入视频流缓存区
                    Array.Copy(startCode, 0, sourceH264Bbytes, index, 4);
                    //将第一片NALU存入视频流缓存区
                    Array.Copy(sourceRtpPackage, 14, sourceH264Bbytes, index+5, length - 14);

                    //索引重新赋值
                    index += length - 14 + 5;
                    break;
                //中间分片
                case 0:
                    Array.Copy(sourceRtpPackage, 14, sourceH264Bbytes, index, length - 14);
                    index += length - 14;
                    break;
                //最后分片
                default:
                    //获取FU Header后五位Type
                    fuHeaderLast5Bit = 0x1f & sourceRtpPackage[13];
                    var naluHeader = Convert.ToByte(fuHeaderLast5Bit + fuIndicatorTop3Bit);
                    //给naluHeader赋值
                    sourceH264Bbytes[naluHeaderIndex] = naluHeader;

                    //将最后一片NALU存入视频流缓存区
                    Array.Copy(sourceRtpPackage, 14, sourceH264Bbytes, index, length - 14);
                    index += length - 14;
                    break;
            }

            return sourceH264Bbytes;








            //return fregmentNaluBytes;
        }



    }
}
