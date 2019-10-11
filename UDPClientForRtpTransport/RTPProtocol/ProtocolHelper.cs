using System;
using System.Threading;

namespace UDPClientTest.RTPProtocol
{
    public class ProtocolHelper
    {
        
        /// <summary>
        /// 获取H.264中的每一个NALU，并同时返回起始码长度
        /// </summary>
        /// <param name="sourcebytes">H.264源码</param>
        /// <param name="index">此时遍历到H.264源码中的索引位置</param>
        /// <param name="startCodeLength">起始码的长度</param>
        /// <returns>去掉起始码的NALU</returns>
        public static byte[] GetPerNALU(byte[] sourcebytes,ref int index,out int startCodeLength)
        {

            //一个NALU的长度
            int oneNaluLength = 0;

            //一个原始NALU的起始字节的长度
            int startByteLength = 0;

            //判断H.264裸码的起始码
            if (!(sourcebytes[0] == 0x00 && sourcebytes[1] == 0x00 && sourcebytes[2] == 0x00 && sourcebytes[3] == 0x01))
            {  
                Console.WriteLine("264源码的NALU的起始字节不为00,00,00,01");
                startCodeLength = startByteLength;
                return null;
            }

            //确定起始码的长度
            startByteLength = sourcebytes[index] == 0x00 && sourcebytes[index + 1] == 0x00 && sourcebytes[index + 2] == 0x00 && sourcebytes[index + 3] == 0x01
                    ? 4 : 3;

            //从除开第一个起始字节开始进行遍历查询到下一个起始码
            for (int i = index+3; i < sourcebytes.Length; i++)
            {
                
                //查询到下一个的起始码
                if ((sourcebytes[i]==0x00&&sourcebytes[i+1]==0x00&&sourcebytes[i+2]==0x00&&sourcebytes[i+3]==0x01)
                    ||(sourcebytes[i] == 0x00 && sourcebytes[i + 1] == 0x00 && sourcebytes[i + 2] == 0x01))
                {
                    oneNaluLength = i - index - startByteLength;
                    break;
                }

                //遍历到最后一个NALU时,即此时为最后一个字节时
                if (i == sourcebytes.Length - 1)
                {
                    oneNaluLength = sourcebytes.Length - index - startByteLength;
                    break;
                }
                
            }

            //建立NALU缓存区
            byte[] naluBuffer = new byte[oneNaluLength];

            //将查询到的一段NALU拷贝进NALU缓存区中
            Array.Copy(sourcebytes, index + startByteLength, naluBuffer, 0, oneNaluLength);

            //输出起始码的长度
            startCodeLength = startByteLength;

            //输出查询到的一个NALU数据包
            return naluBuffer;
        }


        /// <summary>
        /// 单一NALU模式，设置RTP Header并封装成完整的RTP数据包
        /// </summary>
        /// <param name="naluBuffer">获取到的一个长度小于1400的NALU</param>
        /// <param name="sequenceNumber">序列号</param>
        /// <returns>添加了RTP Header的RTP Package</returns>
        public static byte[] RtpPackage(byte[] naluBuffer, ref int sequenceNumber)
        {
            //建立RTP数据包缓存区
            byte[] rtpPackageBytes = new byte[naluBuffer.Length + 12];

            //设置rtpPackageBytes[0]=V_P_X_CC
            rtpPackageBytes[0] = 0x80;
            //设置rtpPackageBytes[1]=M_PT
            rtpPackageBytes[1] = 0xe0;

            //线程安全的自增1
            Interlocked.Increment(ref sequenceNumber);
            if (sequenceNumber > 65535)
            {
                sequenceNumber = 0;
            }
            //设置序列号缓存区，序列号占两个字节
            byte[] sequenceNumberBytes;
            sequenceNumberBytes = BitConverter.GetBytes(sequenceNumber);
            rtpPackageBytes[2] = sequenceNumberBytes[1];
            rtpPackageBytes[3] = sequenceNumberBytes[0];

            //设置时间戳缓存区，占四个字节
            byte[] timeStampBytes;
            timeStampBytes = BitConverter.GetBytes(Convert.ToInt32(DateTime.Now.TimeOfDay.TotalMilliseconds));
            rtpPackageBytes[4] = timeStampBytes[3];
            rtpPackageBytes[5] = timeStampBytes[2];
            rtpPackageBytes[6] = timeStampBytes[1];
            rtpPackageBytes[7] = timeStampBytes[0];

            //设置同步信源标识
            rtpPackageBytes[8] = 0x00;
            rtpPackageBytes[9] = 0x00;
            rtpPackageBytes[10] = 0x00;
            rtpPackageBytes[11] = 0x00;

            Array.Copy(naluBuffer, 0, rtpPackageBytes, 12, naluBuffer.Length);

            return rtpPackageBytes;
        }


        /// <summary>
        /// 分片封包模式
        /// </summary>
        /// <param name="sourceNALU">长度大于1400的NALU</param>
        /// <param name="fregmentNaluLength">当前分片的nalu长度</param>
        /// <param name="totalFregmentNumber">总的分片数量</param>
        /// <param name="currentFregmentNumber">当前分片编号</param>
        /// <param name="sequenceNumber">序列号</param>
        /// <returns>其中的一个分片nalu包,并进行RTP封装</returns>
        public static byte[] FragmentUnit(byte[] sourceNALU, int fregmentNaluLength, int totalFregmentNumber, int currentFregmentNumber, ref int sequenceNumber)
        {
            //首先建立缓存区,总长度包括RTP Header、FU indicator、FU Header、payload
            int startlength = 14;
            if (currentFregmentNumber == 1)
            {
                startlength = 13;
            }
            byte[] fregmentRTPBytes = new byte[fregmentNaluLength + startlength];
            #region 首先设置RTP Header
            //设置V_P_X_CC
            fregmentRTPBytes[0] = 0x80;
            //设置M_PT
            fregmentRTPBytes[1] = 0xe0;

            //建立序列号缓存区，占2字节;设置sequenceNumber
            byte[] sequenceNumberBytes;
            //序列号线程安全自增1
            Interlocked.Increment(ref sequenceNumber);
            if (sequenceNumber > 65535)
            {
                sequenceNumber = 0;
            }
            sequenceNumberBytes = BitConverter.GetBytes(sequenceNumber);
            fregmentRTPBytes[2] = sequenceNumberBytes[1];
            fregmentRTPBytes[3] = sequenceNumberBytes[0];

            //建立时间戳缓存区，占四个字节
            byte[] timeStampBytes;
            timeStampBytes = BitConverter.GetBytes(Convert.ToInt32(DateTime.Now.TimeOfDay.TotalMilliseconds));
            fregmentRTPBytes[4] = timeStampBytes[3];
            fregmentRTPBytes[5] = timeStampBytes[2];
            fregmentRTPBytes[6] = timeStampBytes[1];
            fregmentRTPBytes[7] = timeStampBytes[0];

            //设置同步信源标识
            fregmentRTPBytes[8] = 0x00;
            fregmentRTPBytes[9] = 0x00;
            fregmentRTPBytes[10] = 0x00;
            fregmentRTPBytes[11] = 0x00;

            #endregion

            #region 设置FU indicator和FU Header
            //首先设置FU indicator  F_NRI_TYPE 占1字节
            fregmentRTPBytes[12] = Convert.ToByte((sourceNALU[0] & 0x60) + 28);

            //再设置FU Header  S_E_R_TYPE
            //若为第一分片
            if (currentFregmentNumber == 1)
            {
                fregmentRTPBytes[13] = Convert.ToByte((0x1f & sourceNALU[0]) + 128);
            }
            //若为最后一个分片
            if (currentFregmentNumber == totalFregmentNumber)
            {
                fregmentRTPBytes[13] = Convert.ToByte((0x1f & sourceNALU[0]) + 64);
            }
            //若为中间分片
            fregmentRTPBytes[13] = Convert.ToByte((0x1f & sourceNALU[0]));

            #endregion

            //RTP封包
            //若为第一包
            if (currentFregmentNumber == 1)
            {
                Array.Copy(sourceNALU, 1, fregmentRTPBytes, 14, fregmentNaluLength - 1);
            }
            else
            {
                Array.Copy(sourceNALU, (currentFregmentNumber - 1) * 1400, fregmentRTPBytes, 14, fregmentNaluLength);
            }
            return fregmentRTPBytes;

            


        }

    }
}
