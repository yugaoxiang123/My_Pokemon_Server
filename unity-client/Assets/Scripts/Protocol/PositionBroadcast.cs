using System;
using System.Collections.Generic;
using System.IO;

namespace MyPokemon.Protocol
{
    public class PositionBroadcast
    {
        public List<PlayerPosition> Positions { get; set; } = new List<PlayerPosition>();

        public class PlayerPosition
        {
            public string PlayerId { get; set; }
            public float X { get; set; }
            public float Y { get; set; }
            public int Direction { get; set; }
        }

        // 添加Parser静态属性
        public static MessageParser<PositionBroadcast> Parser { get; } = new MessageParser<PositionBroadcast>(() => new PositionBroadcast());

        // 从字节数组解析消息
        public static PositionBroadcast ParseFrom(byte[] data, int offset, int length)
        {
            var broadcast = new PositionBroadcast();
            int position = offset;
            int endPosition = offset + length;

            while (position < endPosition)
            {
                byte tag = data[position++];
                if (tag == 10) // field 1, wire type 2 (length-delimited)
                {
                    int messageLength = ReadVarint(data, ref position);
                    var playerPos = ParsePlayerPosition(data, position, messageLength);
                    broadcast.Positions.Add(playerPos);
                    position += messageLength;
                }
            }

            return broadcast;
        }

        private static PlayerPosition ParsePlayerPosition(byte[] data, int offset, int length)
        {
            var pos = new PlayerPosition();
            int position = offset;
            int endPosition = offset + length;

            while (position < endPosition)
            {
                byte tag = data[position++];
                switch (tag)
                {
                    case 10: // PlayerId (string)
                        int strLength = ReadVarint(data, ref position);
                        pos.PlayerId = System.Text.Encoding.UTF8.GetString(data, position, strLength);
                        position += strLength;
                        break;

                    case 21: // X (float)
                        pos.X = ReadFloat(data, ref position);
                        break;

                    case 29: // Y (float)
                        pos.Y = ReadFloat(data, ref position);
                        break;

                    case 32: // Direction (varint)
                        pos.Direction = ReadVarint(data, ref position);
                        break;
                }
            }

            return pos;
        }

        private static int ReadVarint(byte[] data, ref int position)
        {
            int result = 0;
            int shift = 0;

            while (true)
            {
                byte b = data[position++];
                result |= (b & 0x7F) << shift;
                if ((b & 0x80) == 0)
                    break;
                shift += 7;
            }

            return result;
        }

        private static float ReadFloat(byte[] data, ref int position)
        {
            byte[] floatBytes = new byte[4];
            if (BitConverter.IsLittleEndian)
            {
                floatBytes[0] = data[position++];
                floatBytes[1] = data[position++];
                floatBytes[2] = data[position++];
                floatBytes[3] = data[position++];
            }
            else
            {
                floatBytes[3] = data[position++];
                floatBytes[2] = data[position++];
                floatBytes[1] = data[position++];
                floatBytes[0] = data[position++];
            }
            return BitConverter.ToSingle(floatBytes, 0);
        }
    }
} 