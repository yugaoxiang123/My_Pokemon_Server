using System;

namespace MyPokemon.Protocol
{
    public class PositionRequest
    {
        public float X { get; set; }        // X坐标
        public float Y { get; set; }        // Y坐标
        public int Direction { get; set; }   // 朝向(0:上, 1:右, 2:下, 3:左)
        public string PlayerId { get; set; } // 玩家ID

        // 默认构造函数
        public PositionRequest()
        {
        }

        // 带参数的构造函数
        public PositionRequest(float x, float y, int direction, string playerId = null)
        {
            X = x;
            Y = y;
            Direction = direction;
            PlayerId = playerId;
        }

        // 序列化为字节数组
        public byte[] ToByteArray()
        {
            // 计算消息大小
            int size = 0;
            if (X != 0F) size += 5;  // 1字节tag + 4字节float
            if (Y != 0F) size += 5;  // 1字节tag + 4字节float
            if (Direction != 0) size += 1 + GetVarintSize(Direction);
            if (!string.IsNullOrEmpty(PlayerId)) size += 1 + GetVarintSize(PlayerId.Length) + PlayerId.Length;

            byte[] buffer = new byte[size];
            int position = 0;

            // 写入X (field number = 1, wire type = 5 fixed32)
            if (X != 0F)
            {
                buffer[position++] = 13;  // (1 << 3) | 5
                WriteFloat(buffer, ref position, X);
            }

            // 写入Y (field number = 2, wire type = 5 fixed32)
            if (Y != 0F)
            {
                buffer[position++] = 21;  // (2 << 3) | 5
                WriteFloat(buffer, ref position, Y);
            }

            // 写入Direction (field number = 3, wire type = 0 varint)
            if (Direction != 0)
            {
                buffer[position++] = 24;  // (3 << 3) | 0
                WriteVarint(buffer, ref position, (uint)Direction);
            }

            // 写入PlayerId (field number = 4, wire type = 2 length-delimited)
            if (!string.IsNullOrEmpty(PlayerId))
            {
                buffer[position++] = 34;  // (4 << 3) | 2
                WriteVarint(buffer, ref position, (uint)PlayerId.Length);
                System.Text.Encoding.UTF8.GetBytes(PlayerId, 0, PlayerId.Length, buffer, position);
                position += PlayerId.Length;
            }

            return buffer;
        }

        private static void WriteFloat(byte[] buffer, ref int position, float value)
        {
            var bytes = BitConverter.GetBytes(value);
            if (BitConverter.IsLittleEndian)
            {
                buffer[position++] = bytes[0];
                buffer[position++] = bytes[1];
                buffer[position++] = bytes[2];
                buffer[position++] = bytes[3];
            }
            else
            {
                buffer[position++] = bytes[3];
                buffer[position++] = bytes[2];
                buffer[position++] = bytes[1];
                buffer[position++] = bytes[0];
            }
        }

        private static void WriteVarint(byte[] buffer, ref int position, uint value)
        {
            while (value > 127)
            {
                buffer[position++] = (byte)((value & 0x7F) | 0x80);
                value >>= 7;
            }
            buffer[position++] = (byte)value;
        }

        private static int GetVarintSize(int value)
        {
            if (value <= 127) return 1;
            if (value <= 16383) return 2;
            if (value <= 2097151) return 3;
            if (value <= 268435455) return 4;
            return 5;
        }
    }
} 