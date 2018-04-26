using Microsoft.AspNetCore.Http.Features;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SidekaApi.Helpers.Buffering
{
    public class SendFileFeatureWrapper : IHttpSendFileFeature
    {
        private readonly IHttpSendFileFeature _originalSendFileFeature;
        private readonly BufferingWriteStream _bufferStream;

        public SendFileFeatureWrapper(IHttpSendFileFeature originalSendFileFeature, BufferingWriteStream bufferStream)
        {
            _originalSendFileFeature = originalSendFileFeature;
            _bufferStream = bufferStream;
        }

        // Flush and disable the buffer if anyone tries to call the SendFile feature.
        public async Task SendFileAsync(string path, long offset, long? length, CancellationToken cancellation)
        {
            await _bufferStream.DisableBufferingAsync(cancellation);
            await _originalSendFileFeature.SendFileAsync(path, offset, length, cancellation);
        }
    }
}
