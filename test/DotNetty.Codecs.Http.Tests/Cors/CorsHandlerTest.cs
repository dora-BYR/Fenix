﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http.Tests.Cors
{
    using System.Collections.Generic;
    using DotNetty.Codecs.Http.Cors;
    using DotNetty.Common.Concurrency;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Channels.Embedded;
    using Xunit;

    public sealed class CorsHandlerTest
    {
        [Fact]
        public void NonCorsRequest()
        {
            IHttpResponse response = SimpleRequest(CorsConfigBuilder.ForAnyOrigin().Build(), null);
            Assert.False(response.Headers.Contains(HttpHeaderNames.AccessControlAllowOrigin));
            Assert.True(ReferenceCountUtil.Release(response));
        }

        [Fact]
        public void SimpleRequestWithAnyOrigin()
        {
            IHttpResponse response = SimpleRequest(CorsConfigBuilder.ForAnyOrigin().Build(), "http://localhost:7777");
            Assert.Equal("*", response.Headers.Get(HttpHeaderNames.AccessControlAllowOrigin, null).ToString());
            Assert.Null(response.Headers.Get(HttpHeaderNames.AccessControlAllowHeaders, null));
            Assert.True(ReferenceCountUtil.Release(response));
        }

        [Fact]
        public void SimpleRequestWithNullOrigin()
        {
            IHttpResponse response = SimpleRequest(CorsConfigBuilder.ForOrigin((AsciiString)"http://test.com")
                .AllowNullOrigin()
                .AllowCredentials()
                .Build(), "null");
            Assert.Equal("null", response.Headers.Get(HttpHeaderNames.AccessControlAllowOrigin, null).ToString());
            Assert.Equal("true", response.Headers.Get(HttpHeaderNames.AccessControlAllowCredentials, null).ToString());
            Assert.Null(response.Headers.Get(HttpHeaderNames.AccessControlAllowHeaders, null));
            Assert.True(ReferenceCountUtil.Release(response));
        }

        [Fact]
        public void SimpleRequestWithOrigin()
        {
            var origin = new AsciiString("http://localhost:8888");
            IHttpResponse response = SimpleRequest(CorsConfigBuilder.ForOrigin(origin).Build(), origin.ToString());
            Assert.Equal(origin, response.Headers.Get(HttpHeaderNames.AccessControlAllowOrigin, null));
            Assert.Null(response.Headers.Get(HttpHeaderNames.AccessControlAllowHeaders, null));
            Assert.True(ReferenceCountUtil.Release(response));
        }

        [Fact]
        public void SimpleRequestWithOrigins()
        {
            var origin1 = new AsciiString("http://localhost:8888");
            var origin2 = new AsciiString("https://localhost:8888");
            ICharSequence[] origins = { origin1, origin2 };
            IHttpResponse response1 = SimpleRequest(CorsConfigBuilder.ForOrigins(origins).Build(), origin1.ToString());
            Assert.Equal(origin1, response1.Headers.Get(HttpHeaderNames.AccessControlAllowOrigin, null));
            Assert.Null(response1.Headers.Get(HttpHeaderNames.AccessControlAllowHeaders, null));
            Assert.True(ReferenceCountUtil.Release(response1));

            IHttpResponse response2 = SimpleRequest(CorsConfigBuilder.ForOrigins(origins).Build(), origin2.ToString());
            Assert.Equal(origin2, response2.Headers.Get(HttpHeaderNames.AccessControlAllowOrigin, null));
            Assert.Null(response2.Headers.Get(HttpHeaderNames.AccessControlAllowHeaders, null));
            Assert.True(ReferenceCountUtil.Release(response2));
        }

        [Fact]
        public void SimpleRequestWithNoMatchingOrigin()
        {
            var origin = new AsciiString("http://localhost:8888");
            IHttpResponse response = SimpleRequest(CorsConfigBuilder.ForOrigins(
                new AsciiString("https://localhost:8888")).Build(), origin.ToString());
            Assert.Null(response.Headers.Get(HttpHeaderNames.AccessControlAllowOrigin, null));
            Assert.Null(response.Headers.Get(HttpHeaderNames.AccessControlAllowHeaders, null));
            Assert.True(ReferenceCountUtil.Release(response));
        }

        [Fact]
        public void PreflightDeleteRequestWithCustomHeaders()
        {
            CorsConfig config = CorsConfigBuilder.ForOrigin(
                new AsciiString("http://localhost:8888")).AllowedRequestMethods(HttpMethod.Get, HttpMethod.Delete).Build();
            IHttpResponse response = PreflightRequest(config, "http://localhost:8888", "content-type, xheader1");
            Assert.Equal("http://localhost:8888", response.Headers.Get(HttpHeaderNames.AccessControlAllowOrigin, null));
            Assert.Contains("GET", response.Headers.Get(HttpHeaderNames.AccessControlAllowMethods, null).ToString());
            Assert.Contains("DELETE", response.Headers.Get(HttpHeaderNames.AccessControlAllowMethods, null).ToString());
            Assert.Equal(HttpHeaderNames.Origin.ToString(), response.Headers.Get(HttpHeaderNames.Vary, null));
            Assert.True(ReferenceCountUtil.Release(response));
        }

        [Fact]
        public void PreflightGetRequestWithCustomHeaders()
        {
            CorsConfig config = CorsConfigBuilder.ForOrigin(new AsciiString("http://localhost:8888"))
                .AllowedRequestMethods(HttpMethod.Options, HttpMethod.Get, HttpMethod.Delete)
                .AllowedRequestHeaders((AsciiString)"content-type", (AsciiString)"xheader1")
                .Build();
            var response = PreflightRequest(config, "http://localhost:8888", "content-type, xheader1");
            Assert.Equal("http://localhost:8888", response.Headers.Get(HttpHeaderNames.AccessControlAllowOrigin, null));
            Assert.Contains("OPTIONS", response.Headers.Get(HttpHeaderNames.AccessControlAllowMethods, null).ToString());
            Assert.Contains("GET", response.Headers.Get(HttpHeaderNames.AccessControlAllowMethods, null).ToString());
            Assert.Contains("content-type", response.Headers.Get(HttpHeaderNames.AccessControlAllowHeaders, null).ToString());
            Assert.Contains("xheader1", response.Headers.Get(HttpHeaderNames.AccessControlAllowHeaders, null).ToString());
            Assert.Equal(HttpHeaderNames.Origin.ToString(), response.Headers.Get(HttpHeaderNames.Vary, null));
            Assert.True(ReferenceCountUtil.Release(response));
        }

        [Fact]
        public void PreflightRequestWithDefaultHeaders()
        {
            CorsConfig config = CorsConfigBuilder.ForOrigin(new AsciiString("http://localhost:8888")).Build();
            var response = PreflightRequest(config, "http://localhost:8888", "content-type, xheader1");
            Assert.Equal("0", response.Headers.Get(HttpHeaderNames.ContentLength, null).ToString());
            Assert.NotNull(response.Headers.Get(HttpHeaderNames.Date, null));
            Assert.Equal(HttpHeaderNames.Origin.ToString(), response.Headers.Get(HttpHeaderNames.Vary, null));
            Assert.True(ReferenceCountUtil.Release(response));
        }

        [Fact]
        public void PreflightRequestWithCustomHeader()
        {
            CorsConfig config = CorsConfigBuilder.ForOrigin(new AsciiString("http://localhost:8888"))
                .PreflightResponseHeader((AsciiString)"CustomHeader", "somevalue")
                .Build();
            var response = PreflightRequest(config, "http://localhost:8888", "content-type, xheader1");
            Assert.Equal("somevalue", response.Headers.Get((AsciiString)"CustomHeader", null).ToString());
            Assert.Equal(HttpHeaderNames.Origin.ToString(), response.Headers.Get(HttpHeaderNames.Vary, null));
            Assert.Equal("0", response.Headers.Get(HttpHeaderNames.ContentLength, null).ToString());
            Assert.True(ReferenceCountUtil.Release(response));
        }

        [Fact]
        public void PreflightRequestWithUnauthorizedOrigin()
        {
            var origin = "http://host";
            CorsConfig config = CorsConfigBuilder.ForOrigin((AsciiString)"http://localhost").Build();
            var response = PreflightRequest(config, origin, "xheader1");
            Assert.False(response.Headers.Contains(HttpHeaderNames.AccessControlAllowOrigin));
            Assert.True(ReferenceCountUtil.Release(response));
        }

        [Fact]
        public void PreflightRequestWithCustomHeaders()
        {
            const string HeaderName = "CustomHeader";
            const string Value1 = "value1";
            const string Value2 = "value2";
            CorsConfig config = CorsConfigBuilder.ForOrigin((AsciiString)"http://localhost:8888")
                .PreflightResponseHeader((AsciiString)HeaderName, (AsciiString)Value1, (AsciiString)Value2).Build();
            IHttpResponse response = PreflightRequest(config, "http://localhost:8888", "content-type, xheader1");
            AssertValues(response, HeaderName, Value1, Value2);
            Assert.Equal(HttpHeaderNames.Origin.ToString(), response.Headers.Get(HttpHeaderNames.Vary, null));
            Assert.Equal("0", response.Headers.Get(HttpHeaderNames.ContentLength, null).ToString());
            Assert.True(ReferenceCountUtil.Release(response));
        }

        [Fact]
        public void PreflightRequestWithCustomHeadersIterable()
        {
            const string HeaderName = "CustomHeader";
            const string Value1 = "value1";
            const string Value2 = "value2";
            CorsConfig config = CorsConfigBuilder.ForOrigin((AsciiString)"http://localhost:8888")
                .PreflightResponseHeader((AsciiString)HeaderName, new List<ICharSequence> { (AsciiString)Value1, (AsciiString)Value2 })
                .Build();
            IHttpResponse response = PreflightRequest(config, "http://localhost:8888", "content-type, xheader1");
            AssertValues(response, HeaderName, Value1, Value2);
            Assert.Equal(HttpHeaderNames.Origin.ToString(), response.Headers.Get(HttpHeaderNames.Vary, null));
            Assert.True(ReferenceCountUtil.Release(response));
        }

        class ValueGenerator : ICallable<object>
        {
            public object Call() => "generatedValue";
        }

        [Fact]
        public void PreflightRequestWithValueGenerator()
        {
            CorsConfig config = CorsConfigBuilder.ForOrigin((AsciiString)"http://localhost:8888")
                .PreflightResponseHeader((AsciiString)"GenHeader", new ValueGenerator()).Build();
            IHttpResponse response = PreflightRequest(config, "http://localhost:8888", "content-type, xheader1");
            Assert.Equal("generatedValue", response.Headers.Get((AsciiString)"GenHeader", null).ToString());
            Assert.Equal(HttpHeaderNames.Origin.ToString(), response.Headers.Get(HttpHeaderNames.Vary, null));
            Assert.True(ReferenceCountUtil.Release(response));
        }

        [Fact]
        public void PreflightRequestWithNullOrigin()
        {
            var origin = new AsciiString("null");
            CorsConfig config = CorsConfigBuilder.ForOrigin(origin)
                    .AllowNullOrigin()
                    .AllowCredentials()
                    .Build();
            IHttpResponse response = PreflightRequest(config, origin.ToString(), "content-type, xheader1");
            Assert.Equal("null", response.Headers.Get(HttpHeaderNames.AccessControlAllowOrigin, null));
            Assert.Equal("true", response.Headers.Get(HttpHeaderNames.AccessControlAllowCredentials, null));
            Assert.True(ReferenceCountUtil.Release(response));
        }

        [Fact]
        public void PreflightRequestAllowCredentials()
        {
            var origin = new AsciiString("null");
            CorsConfig config = CorsConfigBuilder.ForOrigin(origin).AllowCredentials().Build();
            IHttpResponse response = PreflightRequest(config, origin.ToString(), "content-type, xheader1");
            Assert.Equal("true", response.Headers.Get(HttpHeaderNames.AccessControlAllowCredentials, null));
            Assert.True(ReferenceCountUtil.Release(response));
        }

        [Fact]
        public void PreflightRequestDoNotAllowCredentials()
        {
            CorsConfig config = CorsConfigBuilder.ForOrigin((AsciiString)"http://localhost:8888").Build();
            IHttpResponse response = PreflightRequest(config, "http://localhost:8888", "");
            // the only valid value for Access-Control-Allow-Credentials is true.
            Assert.False(response.Headers.Contains(HttpHeaderNames.AccessControlAllowCredentials));
            Assert.True(ReferenceCountUtil.Release(response));
        }

        [Fact]
        public void SimpleRequestCustomHeaders()
        {
            CorsConfig config = CorsConfigBuilder.ForAnyOrigin()
                .ExposeHeaders((AsciiString)"custom1", (AsciiString)"custom2").Build();
            IHttpResponse response = SimpleRequest(config, "http://localhost:7777");
            Assert.Equal("*", response.Headers.Get(HttpHeaderNames.AccessControlAllowOrigin, null));
            Assert.Contains("custom1", response.Headers.Get(HttpHeaderNames.AccessControlExposeHeaders, null).ToString());
            Assert.Contains("custom2", response.Headers.Get(HttpHeaderNames.AccessControlExposeHeaders, null).ToString());
            Assert.True(ReferenceCountUtil.Release(response));
        }

        [Fact]
        public void SimpleRequestAllowCredentials()
        {
            CorsConfig config = CorsConfigBuilder.ForAnyOrigin().AllowCredentials().Build();
            IHttpResponse response = SimpleRequest(config, "http://localhost:7777");
            Assert.Equal("true", response.Headers.Get(HttpHeaderNames.AccessControlAllowCredentials, null));
            Assert.True(ReferenceCountUtil.Release(response));
        }

        [Fact]
        public void SimpleRequestDoNotAllowCredentials()
        {
            CorsConfig config = CorsConfigBuilder.ForAnyOrigin().Build();
            IHttpResponse response = SimpleRequest(config, "http://localhost:7777");
            Assert.False(response.Headers.Contains(HttpHeaderNames.AccessControlAllowCredentials));
            Assert.True(ReferenceCountUtil.Release(response));
        }

        [Fact]
        public void AnyOriginAndAllowCredentialsShouldEchoRequestOrigin()
        {
            CorsConfig config = CorsConfigBuilder.ForAnyOrigin().AllowCredentials().Build();
            IHttpResponse response = SimpleRequest(config, "http://localhost:7777");
            Assert.Equal("true", response.Headers.Get(HttpHeaderNames.AccessControlAllowCredentials, null));
            Assert.Equal("http://localhost:7777", response.Headers.Get(HttpHeaderNames.AccessControlAllowOrigin, null).ToString());
            Assert.Equal(HttpHeaderNames.Origin.ToString(), response.Headers.Get(HttpHeaderNames.Vary, null));
            Assert.True(ReferenceCountUtil.Release(response));
        }

        [Fact]
        public void SimpleRequestExposeHeaders()
        {
            CorsConfig config = CorsConfigBuilder.ForAnyOrigin()
                .ExposeHeaders((AsciiString)"one", (AsciiString)"two").Build();
            IHttpResponse response = SimpleRequest(config, "http://localhost:7777");
            Assert.Contains("one", response.Headers.Get(HttpHeaderNames.AccessControlExposeHeaders, null).ToString());
            Assert.Contains("two", response.Headers.Get(HttpHeaderNames.AccessControlExposeHeaders, null).ToString());
            Assert.True(ReferenceCountUtil.Release(response));
        }

        [Fact]
        public void SimpleRequestShortCircuit()
        {
            CorsConfig config = CorsConfigBuilder.ForOrigin((AsciiString)"http://localhost:8080")
                .ShortCircuit().Build();
            IHttpResponse response = SimpleRequest(config, "http://localhost:7777");
            Assert.Equal(HttpResponseStatus.Forbidden, response.Status);
            Assert.Equal("0", response.Headers.Get(HttpHeaderNames.ContentLength, null).ToString());
            Assert.True(ReferenceCountUtil.Release(response));
        }

        [Fact]
        public void SimpleRequestNoShortCircuit()
        {
            CorsConfig config = CorsConfigBuilder.ForOrigin((AsciiString)"http://localhost:8080").Build();
            IHttpResponse response = SimpleRequest(config, "http://localhost:7777");
            Assert.Equal(HttpResponseStatus.OK, response.Status);
            Assert.Null(response.Headers.Get(HttpHeaderNames.AccessControlAllowOrigin, null));
            Assert.True(ReferenceCountUtil.Release(response));
        }

        [Fact]
        public void ShortCircuitNonCorsRequest()
        {
            CorsConfig config = CorsConfigBuilder.ForOrigin((AsciiString)"https://localhost")
                .ShortCircuit().Build();
            IHttpResponse response = SimpleRequest(config, null);
            Assert.Equal(HttpResponseStatus.OK, response.Status);
            Assert.Null(response.Headers.Get(HttpHeaderNames.AccessControlAllowOrigin, null));
            Assert.True(ReferenceCountUtil.Release(response));
        }

        [Fact]
        public void ShortCircuitWithConnectionKeepAliveShouldStayOpen()
        {
            CorsConfig config = CorsConfigBuilder.ForOrigin((AsciiString)"http://localhost:8080")
                .ShortCircuit().Build();
            var channel = new EmbeddedChannel(new CorsHandler(config));
            IFullHttpRequest request = CreateHttpRequest(HttpMethod.Get);
            request.Headers.Set(HttpHeaderNames.Origin, (AsciiString)"http://localhost:8888");
            request.Headers.Set(HttpHeaderNames.Connection, HttpHeaderValues.KeepAlive);

            Assert.False(channel.WriteInbound(request));
            var response = channel.ReadOutbound<IHttpResponse>();
            Assert.True(HttpUtil.IsKeepAlive(response));

            Assert.True(channel.Open);
            Assert.Equal(HttpResponseStatus.Forbidden, response.Status);
            Assert.True(ReferenceCountUtil.Release(response));
            Assert.False(channel.Finish());
        }

        [Fact]
        public void ShortCircuitWithoutConnectionShouldStayOpen()
        {
            CorsConfig config = CorsConfigBuilder.ForOrigin((AsciiString)"http://localhost:8080")
                .ShortCircuit().Build();
            var channel = new EmbeddedChannel(new CorsHandler(config));
            IFullHttpRequest request = CreateHttpRequest(HttpMethod.Get);
            request.Headers.Set(HttpHeaderNames.Origin, (AsciiString)"http://localhost:8888");

            Assert.False(channel.WriteInbound(request));
            var response = channel.ReadOutbound<IHttpResponse>();
            Assert.True(HttpUtil.IsKeepAlive(response));

            Assert.True(channel.Open);
            Assert.Equal(HttpResponseStatus.Forbidden, response.Status);
            Assert.True(ReferenceCountUtil.Release(response));
            Assert.False(channel.Finish());
        }

        [Fact]
        public void ShortCircuitWithConnectionCloseShouldClose()
        {
            CorsConfig config = CorsConfigBuilder.ForOrigin((AsciiString)"http://localhost:8080")
                .ShortCircuit().Build();
            var channel = new EmbeddedChannel(new CorsHandler(config));
            IFullHttpRequest request = CreateHttpRequest(HttpMethod.Get);
            request.Headers.Set(HttpHeaderNames.Origin, "http://localhost:8888");
            request.Headers.Set(HttpHeaderNames.Connection, HttpHeaderValues.Close);

            Assert.False(channel.WriteInbound(request));
            var response = channel.ReadOutbound<IHttpResponse>();
            Assert.False(HttpUtil.IsKeepAlive(response));

            Assert.False(channel.Open);
            Assert.Equal(HttpResponseStatus.Forbidden, response.Status);
            Assert.True(ReferenceCountUtil.Release(response));
            Assert.False(channel.Finish());
        }

        [Fact]
        public void PreflightRequestShouldReleaseRequest()
        {
            CorsConfig config = CorsConfigBuilder.ForOrigin((AsciiString)"http://localhost:8888")
                    .PreflightResponseHeader((AsciiString)"CustomHeader", new List<ICharSequence> { (AsciiString)"value1", (AsciiString)"value2" })
                    .Build();
            var channel = new EmbeddedChannel(new CorsHandler(config));
            IFullHttpRequest request = OptionsRequest("http://localhost:8888", "content-type, xheader1", null);
            Assert.False(channel.WriteInbound(request));
            Assert.Equal(0, request.ReferenceCount);
            Assert.True(ReferenceCountUtil.Release(channel.ReadOutbound<object>()));
            Assert.False(channel.Finish());
        }

        [Fact]
        public void PreflightRequestWithConnectionKeepAliveShouldStayOpen()
        {
            CorsConfig config = CorsConfigBuilder.ForOrigin((AsciiString)"http://localhost:8888").Build();
            var channel = new EmbeddedChannel(new CorsHandler(config));
            IFullHttpRequest request = OptionsRequest("http://localhost:8888", "", HttpHeaderValues.KeepAlive);
            Assert.False(channel.WriteInbound(request));
            var response = channel.ReadOutbound<IHttpResponse>();
            Assert.True(HttpUtil.IsKeepAlive(response));

            Assert.True(channel.Open);
            Assert.Equal(HttpResponseStatus.OK, response.Status);
            Assert.True(ReferenceCountUtil.Release(response));
            Assert.False(channel.Finish());
        }

        [Fact]
        public void PreflightRequestWithoutConnectionShouldStayOpen()
        {
            CorsConfig config = CorsConfigBuilder.ForOrigin((AsciiString)"http://localhost:8888").Build();
            var channel = new EmbeddedChannel(new CorsHandler(config));
            IFullHttpRequest request = OptionsRequest("http://localhost:8888", "", null);
            Assert.False(channel.WriteInbound(request));
            var response = channel.ReadOutbound<IHttpResponse>();
            Assert.True(HttpUtil.IsKeepAlive(response));

            Assert.True(channel.Open);
            Assert.Equal(HttpResponseStatus.OK, response.Status);
            Assert.True(ReferenceCountUtil.Release(response));
            Assert.False(channel.Finish());
        }

        [Fact]
        public void PreflightRequestWithConnectionCloseShouldClose()
        {
            CorsConfig config = CorsConfigBuilder.ForOrigin((AsciiString)"http://localhost:8888").Build();
            var channel = new EmbeddedChannel(new CorsHandler(config));
            IFullHttpRequest request = OptionsRequest("http://localhost:8888", "", HttpHeaderValues.Close);
            Assert.False(channel.WriteInbound(request));
            var response = channel.ReadOutbound<IHttpResponse>();
            Assert.False(HttpUtil.IsKeepAlive(response));

            Assert.False(channel.Open);
            Assert.Equal(HttpResponseStatus.OK, response.Status);
            Assert.True(ReferenceCountUtil.Release(response));
            Assert.False(channel.Finish());
        }

        [Fact]
        public void ForbiddenShouldReleaseRequest()
        {
            CorsConfig config = CorsConfigBuilder.ForOrigin((AsciiString)"https://localhost").ShortCircuit().Build();
            var channel = new EmbeddedChannel(new CorsHandler(config), new EchoHandler());
            IFullHttpRequest request = CreateHttpRequest(HttpMethod.Get);
            request.Headers.Set(HttpHeaderNames.Origin, "http://localhost:8888");
            Assert.False(channel.WriteInbound(request));
            Assert.Equal(0, request.ReferenceCount);
            Assert.True(ReferenceCountUtil.Release(channel.ReadOutbound<object>()));
            Assert.False(channel.Finish());
        }

        [Fact]
        public void DifferentConfigsPerOrigin()
        {
            var host1 = "http://host1:80";
            var host2 = "http://host2";
            CorsConfig rule1 = CorsConfigBuilder.ForOrigin((AsciiString)host1).AllowedRequestMethods(HttpMethod.Get).Build();
            CorsConfig rule2 = CorsConfigBuilder.ForOrigin((AsciiString)host2).AllowedRequestMethods(HttpMethod.Get, HttpMethod.Post)
                    .AllowCredentials().Build();

            List<CorsConfig> corsConfigs = new List<CorsConfig>(new[] { rule1, rule2 });

            var preFlightHost1 = PreflightRequest(corsConfigs, host1, "", false);

            Assert.Equal("GET", preFlightHost1.Headers.Get(HttpHeaderNames.AccessControlAllowMethods, null).ToString());
            Assert.Null(preFlightHost1.Headers.GetAsString(HttpHeaderNames.AccessControlAllowCredentials));

            var preFlightHost2 = PreflightRequest(corsConfigs, host2, "", false);
            AssertValues(preFlightHost2, HttpHeaderNames.AccessControlAllowMethods.ToString(), "GET", "POST");
            Assert.Equal("true", preFlightHost2.Headers.GetAsString(HttpHeaderNames.AccessControlAllowCredentials));
        }

        [Fact]
        public void SpecificConfigPrecedenceOverGeneric()
        {
            var host1 = "http://host1";
            var host2 = "http://host2";

            CorsConfig forHost1 = CorsConfigBuilder.ForOrigin((AsciiString)host1).AllowedRequestMethods(HttpMethod.Get).MaxAge(3600L).Build();
            CorsConfig allowAll = CorsConfigBuilder.ForAnyOrigin().AllowedRequestMethods(HttpMethod.Post, HttpMethod.Get, HttpMethod.Options)
                    .MaxAge(1800).Build();

            List<CorsConfig> rules = new List<CorsConfig>(new[] { forHost1, allowAll });

            var host1Response = PreflightRequest(rules, host1, "", false);
            Assert.Equal("GET", host1Response.Headers.Get(HttpHeaderNames.AccessControlAllowMethods, null).ToString());
            Assert.Equal("3600", host1Response.Headers.GetAsString(HttpHeaderNames.AccessControlMaxAge));

            var host2Response = PreflightRequest(rules, host2, "", false);
            AssertValues(host2Response, HttpHeaderNames.AccessControlAllowMethods.ToString(), "POST", "GET", "OPTIONS");
            Assert.Equal("*", host2Response.Headers.GetAsString(HttpHeaderNames.AccessControlAllowOrigin));
            Assert.Equal("1800", host2Response.Headers.GetAsString(HttpHeaderNames.AccessControlMaxAge));
        }

        static IHttpResponse SimpleRequest(CorsConfig config, string origin, string requestHeaders = null) =>
            SimpleRequest(config, origin, requestHeaders, HttpMethod.Get);

        static IHttpResponse SimpleRequest(CorsConfig config, string origin, string requestHeaders, HttpMethod method)
        {
            var channel = new EmbeddedChannel(new CorsHandler(config), new EchoHandler());
            IFullHttpRequest httpRequest = CreateHttpRequest(method);
            if (origin != null)
            {
                httpRequest.Headers.Set(HttpHeaderNames.Origin, new AsciiString(origin));
            }
            if (requestHeaders != null)
            {
                httpRequest.Headers.Set(HttpHeaderNames.AccessControlRequestHeaders, new AsciiString(requestHeaders));
            }

            Assert.False(channel.WriteInbound(httpRequest));
            var response = channel.ReadOutbound<IHttpResponse>();
            Assert.False(channel.Finish());
            return response;
        }

        static IHttpResponse PreflightRequest(CorsConfig config, string origin, string requestHeaders)
        {
            return PreflightRequest(new List<CorsConfig>(new[] { config }), origin, requestHeaders, config.IsShortCircuit);
        }

        static IHttpResponse PreflightRequest(List<CorsConfig> configs, string origin, string requestHeaders, bool isSHortCircuit)
        {
            var channel = new EmbeddedChannel(new CorsHandler(configs, isSHortCircuit));
            Assert.False(channel.WriteInbound(OptionsRequest(origin, requestHeaders, null)));
            var response = channel.ReadOutbound<IHttpResponse>();
            Assert.False(channel.Finish());
            return response;
        }

        static IFullHttpRequest OptionsRequest(string origin, string requestHeaders, AsciiString connection)
        {
            IFullHttpRequest httpRequest = CreateHttpRequest(HttpMethod.Options);
            httpRequest.Headers.Set(HttpHeaderNames.Origin, new AsciiString(origin));
            httpRequest.Headers.Set(HttpHeaderNames.AccessControlRequestMethod, httpRequest.Method);
            httpRequest.Headers.Set(HttpHeaderNames.AccessControlRequestHeaders, new AsciiString(requestHeaders));
            if (connection != null)
            {
                httpRequest.Headers.Set(HttpHeaderNames.Connection, connection);
            }

            return httpRequest;
        }

        static IFullHttpRequest CreateHttpRequest(HttpMethod method) => new DefaultFullHttpRequest(HttpVersion.Http11, method, "/info");

        sealed class EchoHandler : SimpleChannelInboundHandler<object>
        {
            protected override void ChannelRead0(IChannelHandlerContext ctx, object msg) =>
                ctx.WriteAndFlushAsync(new DefaultFullHttpResponse(HttpVersion.Http11, HttpResponseStatus.OK, true, true));
        }

        static void AssertValues(IHttpResponse response, string headerName, params string[] values)
        {
            ICharSequence header = response.Headers.Get(new AsciiString(headerName), null);
            foreach (string value in values)
            {
                Assert.Contains(value, header.ToString());
            }
        }
    }
}
