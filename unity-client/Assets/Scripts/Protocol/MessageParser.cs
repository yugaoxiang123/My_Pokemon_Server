namespace MyPokemon.Protocol
{
    public class MessageParser<T> where T : class
    {
        private readonly System.Func<T> factory;

        public MessageParser(System.Func<T> factory)
        {
            this.factory = factory;
        }

        public T ParseFrom(byte[] data, int offset, int length)
        {
            if (typeof(T) == typeof(PositionBroadcast))
            {
                return PositionBroadcast.ParseFrom(data, offset, length) as T;
            }
            return null;
        }
    }
} 