﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http.WebSockets
{
    using DotNetty.Common.Utilities;

    public sealed class WebSocketVersion
    {
        public static readonly WebSocketVersion Unknown = new WebSocketVersion(string.Empty);

        // http://tools.ietf.org/html/draft-ietf-hybi-thewebsocketprotocol-00
        // draft-ietf-hybi-thewebsocketprotocol- 00.
        public static readonly WebSocketVersion V00 = new WebSocketVersion("0");

        // http://tools.ietf.org/html/draft-ietf-hybi-thewebsocketprotocol-07
        // draft-ietf-hybi-thewebsocketprotocol- 07
        public static readonly WebSocketVersion V07 = new WebSocketVersion("7");

        // http://tools.ietf.org/html/draft-ietf-hybi-thewebsocketprotocol-10
        // draft-ietf-hybi-thewebsocketprotocol- 10
        public static readonly WebSocketVersion V08 = new WebSocketVersion("8");

        // http://tools.ietf.org/html/rfc6455 This was originally
        // http://tools.ietf.org/html/draft-ietf-hybi-thewebsocketprotocol-17 
        //draft-ietf-hybi-thewebsocketprotocol- 17>
        public static readonly WebSocketVersion V13 = new WebSocketVersion("13");

        private readonly AsciiString _value;

        WebSocketVersion(string value)
        {
            _value = AsciiString.Cached(value);
        }

        public override string ToString() => _value.ToString();

        public AsciiString ToHttpHeaderValue()
        {
            if (this == Unknown) ThrowHelper.ThrowInvalidOperationException_UnknownWebSocketVersion();
            return _value;
        }
    }
}
