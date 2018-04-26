using Microsoft.AspNetCore.Http.Features;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SidekaApi.Helpers.Buffering
{
    public class HttpBufferingFeature : IHttpBufferingFeature
    {
        private readonly BufferingWriteStream _buffer;
        private readonly IHttpBufferingFeature _innerFeature;

        internal HttpBufferingFeature(BufferingWriteStream buffer, IHttpBufferingFeature innerFeature)
        {
            _buffer = buffer;
            _innerFeature = innerFeature;
        }

        public void DisableRequestBuffering()
        {
            _innerFeature?.DisableRequestBuffering();
        }

        public void DisableResponseBuffering()
        {
            _buffer.DisableBuffering();
            _innerFeature?.DisableResponseBuffering();
        }
    }
}
