using System.Collections.Generic;
using System.Linq;

namespace IntelOrca.Biohazard.Script.Compilation
{
    internal class TokenReader
    {
        private readonly IEnumerator<Token> _source;
        private readonly LinkedList<Token> _queue = new LinkedList<Token>();
        private Token? _eofToken;

        public TokenReader(IEnumerable<Token> source)
        {
            _source = source.GetEnumerator();
        }

        private Token ReadInternal()
        {
            if (_eofToken.HasValue)
            {
                return _eofToken.Value;
            }
            if (_source.MoveNext())
            {
                var result = _source.Current;
                if (result.Kind == TokenKind.EOF)
                {
                    _eofToken = result;
                }
                return result;
            }
            else
            {
                _eofToken = new Token(TokenKind.EOF, "", 0, 0);
                return _eofToken.Value;
            }
        }

        public Token Peek(int index = 0)
        {
            while (_queue.Count <= index)
            {
                _queue.AddLast(ReadInternal());
            }
            return _queue.ElementAt(index);
        }

        public Token Read()
        {
            Peek();
            var t = _queue.First();
            _queue.RemoveFirst();
            return t;
        }

        public void SkipWhitespace()
        {
            Token result;
            while ((result = Peek()).Kind == TokenKind.Whitespace)
            {
                Read();
            }
        }

        public Token ReadNoWhitespace()
        {
            Token result;
            while ((result = Read()).Kind == TokenKind.Whitespace)
            {
            }
            return result;
        }
    }
}
