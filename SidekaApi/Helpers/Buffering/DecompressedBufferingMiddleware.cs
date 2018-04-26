using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SidekaApi.Helpers.Buffering
{
    public class DecompressedBufferingMiddleware
    {
        private readonly RequestDelegate _next;

        public DecompressedBufferingMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task Invoke(HttpContext httpContext)
        {
            var originalResponseBody = httpContext.Response.Body;

            // no-op if buffering is already available.
            if (originalResponseBody.CanSeek)
            {
                await _next(httpContext);
                return;
            }

            var originalBufferingFeature = httpContext.Features.Get<IHttpBufferingFeature>();
            var originalSendFileFeature = httpContext.Features.Get<IHttpSendFileFeature>();
            try
            {
                // Shim the response stream
                var bufferStream = new BufferingWriteStream(originalResponseBody);
                httpContext.Response.Body = bufferStream;
                httpContext.Features.Set<IHttpBufferingFeature>(new HttpBufferingFeature(bufferStream, originalBufferingFeature));
                if (originalSendFileFeature != null)
                {
                    httpContext.Features.Set<IHttpSendFileFeature>(new SendFileFeatureWrapper(originalSendFileFeature, bufferStream));
                }

                await _next(httpContext);

                // If we're still buffered, set the content-length header and flush the buffer.
                // Only if the content-length header is not already set, and some content was buffered.
                if (!httpContext.Response.HasStarted && bufferStream.CanSeek && bufferStream.Length > 0)
                {
                    if (!httpContext.Response.ContentLength.HasValue)
                    {
                        httpContext.Response.Headers["X-Decompressed-Content-Length"] = bufferStream.Length.ToString();
                    }
                    await bufferStream.FlushAsync();
                }
            }
            finally
            {
                // undo everything
                httpContext.Features.Set(originalBufferingFeature);
                httpContext.Features.Set(originalSendFileFeature);
                httpContext.Response.Body = originalResponseBody;
            }
        }
    }
}
