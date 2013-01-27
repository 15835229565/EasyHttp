﻿#region License

// Distributed under the BSD License
//   
// YouTrackSharp Copyright (c) 2010-2012, Hadi Hariri and Contributors
// All rights reserved.
//   
//  Redistribution and use in source and binary forms, with or without
//  modification, are permitted provided that the following conditions are met:
//      * Redistributions of source code must retain the above copyright
//         notice, this list of conditions and the following disclaimer.
//      * Redistributions in binary form must reproduce the above copyright
//         notice, this list of conditions and the following disclaimer in the
//         documentation and/or other materials provided with the distribution.
//      * Neither the name of Hadi Hariri nor the
//         names of its contributors may be used to endorse or promote products
//         derived from this software without specific prior written permission.
//   
//   THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
//   "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED
//   TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A 
//   PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL 
//   <COPYRIGHTHOLDER> BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
//   SPECIAL,EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT
//   LIMITED  TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE,
//   DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND  ON ANY
//   THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
//   (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF
//   THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
//   

#endregion

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Cache;
using System.Net.Security;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using EasyHttp.Codecs;
using EasyHttp.Infrastructure;

namespace EasyHttp.Http
{
    // TODO: This class needs cleaning up and abstracting the encoder one more level
    public class HttpRequest
    {
        readonly IEncoder _encoder;
        HttpRequestCachePolicy _cachePolicy;
        bool _forceBasicAuth;
        string _password;
        string _username;
        HttpWebRequest httpWebRequest;

        public HttpRequest(IEncoder encoder)
        {
            RawHeaders = new Dictionary<string, object>();

            ClientCertificates = new X509CertificateCollection();

            UserAgent = String.Format("EasyHttp HttpClient v{0}",
                                      Assembly.GetAssembly(typeof (HttpClient)).GetName().Version);

            Accept = String.Join(";", HttpContentTypes.TextHtml, HttpContentTypes.ApplicationXml,
                                 HttpContentTypes.ApplicationJson);
            _encoder = encoder;

            Timeout = 100000; //http://msdn.microsoft.com/en-us/library/system.net.httpwebrequest.timeout.aspx
        }

        public string Accept { get; set; }
        public string AcceptCharSet { get; set; }
        public string AcceptEncoding { get; set; }
        public string AcceptLanguage { get; set; }
        public bool KeepAlive { get; set; }
        public X509CertificateCollection ClientCertificates { get; set; }
        public string ContentLength { get; private set; }
        public string ContentType { get; set; }
        public string ContentEncoding { get; set; }
        public CookieCollection Cookies { get; set; }
        public DateTime Date { get; set; }
        public bool Expect { get; set; }
        public string From { get; set; }
        public string Host { get; set; }
        public string IfMatch { get; set; }
        public DateTime IfModifiedSince { get; set; }
        public string IfRange { get; set; }
        public int MaxForwards { get; set; }
        public string Referer { get; set; }
        public int Range { get; set; }
        public string UserAgent { get; set; }
        public IDictionary<string, object> RawHeaders { get; private set; }
        public HttpMethod Method { get; set; }
        public object Data { get; set; }
        public string Uri { get; set; }
        public string PutFilename { get; set; }
        public IDictionary<string, object> MultiPartFormData { get; set; }
        public IList<FileData> MultiPartFileData { get; set; }
        public int Timeout { get; set; }
        public Boolean ParametersAsSegments { get; set; }

        public bool ForceBasicAuth
        {
            get { return _forceBasicAuth; }
            set { _forceBasicAuth = value; }
        }



        public void SetBasicAuthentication(string username, string password)
        {
            _username = username;
            _password = password;
        }

        void SetupHeader()
        {
            var cookieContainer = new CookieContainer();

            httpWebRequest.CookieContainer = cookieContainer;
            httpWebRequest.ContentType = ContentType;
            httpWebRequest.Accept = Accept;
            httpWebRequest.Method = Method.ToString();
            httpWebRequest.UserAgent = UserAgent;
            httpWebRequest.Referer = Referer;
            httpWebRequest.CachePolicy = _cachePolicy;
            httpWebRequest.KeepAlive = KeepAlive;

            ServicePointManager.Expect100Continue = Expect;
            ServicePointManager.ServerCertificateValidationCallback = AcceptAllCertifications;

            if (Timeout > 0)
            {
                httpWebRequest.Timeout = Timeout;
            }


            if (Cookies != null)
            {
                httpWebRequest.CookieContainer.Add(Cookies);
            }

            if (IfModifiedSince != DateTime.MinValue)
            {
                httpWebRequest.IfModifiedSince = IfModifiedSince;
            }


            if (Date != DateTime.MinValue)
            {
                httpWebRequest.Date = Date;
            }

            if (!String.IsNullOrEmpty(Host))
            {
                httpWebRequest.Host = Host;
            }

            if (MaxForwards != 0)
            {
                httpWebRequest.MaximumAutomaticRedirections = MaxForwards;
            }

            if (Range != 0)
            {
                httpWebRequest.AddRange(Range);
            }

            SetupAuthentication();

            AddExtraHeader("From", From);
            AddExtraHeader("Accept-CharSet", AcceptCharSet);
            AddExtraHeader("Accept-Encoding", AcceptEncoding);
            AddExtraHeader("Accept-Language", AcceptLanguage);
            AddExtraHeader("If-Match", IfMatch);
            AddExtraHeader("Content-Encoding", ContentEncoding);

            foreach (var header in RawHeaders)
            {
                httpWebRequest.Headers.Add(String.Format("{0}: {1}", header.Key, header.Value));
            }
        }

        bool AcceptAllCertifications(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslpolicyerrors)
        {
            return true;
        }

        public void AddExtraHeader(string header, object value)
        {
            if (value != null && !RawHeaders.ContainsKey(header))
            {
                RawHeaders.Add(header, value);
            }
        }

        void SetupBody()
        {
            if (Data != null)
            {
                SetupData();

                return;
            }

            if (!String.IsNullOrEmpty(PutFilename))
            {
                SetupPutFilename();
                return;
            }

            if (MultiPartFormData != null || MultiPartFileData != null)
            {
                SetupMultiPartBody();
            }
        }

        void SetupData()
        {
            var bytes = _encoder.Encode(Data, ContentType);

            var requestStream = httpWebRequest.GetRequestStream();

            
            requestStream.Write(bytes, 0, bytes.Length);

            requestStream.Close();

            httpWebRequest.ContentLength = bytes.Length;
        }

        void SetupPutFilename()
        {
            var requestStream = httpWebRequest.GetRequestStream();

            using (var fileStream = new FileStream(PutFilename, FileMode.Open))
            {
                var buffer = new byte[81982];

                int bytesRead = fileStream.Read(buffer, 0, buffer.Length);
                while (bytesRead > 0)
                {
                    requestStream.Write(buffer, 0, bytesRead);
                    bytesRead = fileStream.Read(buffer, 0, buffer.Length);
                }
                requestStream.Close();
                httpWebRequest.ContentLength = bytesRead;
            }
        }

   
        // TODO:Refactor this. Clean it up. Move WriteString +Count to method, etc. 
        void SetupMultiPartBody()
        {
            var requestStream = httpWebRequest.GetRequestStream();

            var boundayCode = DateTime.Now.Ticks.GetHashCode() + "548130";

            var boundary = string.Format("----------------{0}", boundayCode);
            var boundaryLength = boundary.Length;

            httpWebRequest.ContentType = string.Format("multipart/form-data; boundary=--------------{0}", boundayCode);

            requestStream.WriteString(boundary);

            var contentLength = boundaryLength;

            if (MultiPartFormData != null)
            {
                foreach (var entry in MultiPartFormData)
                {
                    requestStream.WriteString("\r\n");
                    contentLength += 2;

                    var boundaryHeader = string.Format("Content-Disposition: form-data; name=\"{0}\"\r\n\r\n{1}\r\n", entry.Key, entry.Value);
                    requestStream.WriteString(boundaryHeader);
                    contentLength += boundaryHeader.Length;

                    requestStream.WriteString(boundary);
                    contentLength += boundaryLength;
                }
            }


            if (MultiPartFileData != null)
            {
                foreach (var fileData in MultiPartFileData)
                {
                    using (var file = new FileStream(fileData.Filename, FileMode.Open))
                    {
                        requestStream.WriteString("\r\n");
                        contentLength += 2;

                        string filename = Path.GetFileName(fileData.Filename);
                        var boundaryHeader = string.Format("Content-Disposition: form-data; name=\"{0}\"; filename=\"{1}\"\r\n", fileData.FieldName, filename);
                        requestStream.WriteString(
                            boundaryHeader);
                        contentLength += boundaryHeader.Length;
                        var boundaryContentType = string.Format("Content-Type: {0}\r\n", fileData.ContentType);
                        requestStream.WriteString(boundaryContentType);
                        contentLength += boundaryContentType.Length;
                        var boundaryTransferEncoding = string.Format("Content-Transfer-Encoding: {0}\r\n", fileData.ContentTransferEncoding);
                        requestStream.WriteString(boundaryTransferEncoding);
                        contentLength += boundaryTransferEncoding.Length; // Not sure if this is correct since if transfer-encoding, content-length shouldn't be set?

                        requestStream.WriteString("\r\n");
                        contentLength += 2;
                        var buffer = new byte[8192];

                        int count = 0;

                        while ((count = file.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            if (fileData.ContentTransferEncoding == HttpContentEncoding.Base64)
                            {
                                var str = Convert.ToBase64String(buffer, 0, count);

                                requestStream.WriteString(str);
                            }
                            else if (fileData.ContentTransferEncoding == HttpContentEncoding.Binary)
                            {
                                requestStream.Write(buffer, 0, count);
                            }
                        }
                        contentLength += count;
                        requestStream.WriteString("\r\n");
                        contentLength += 2;
                        requestStream.WriteString(boundary);
                        contentLength += boundaryLength;
                    }
                }
                requestStream.WriteString("--");
                contentLength += 2;
            }
            else
            {
                if (MultiPartFormData != null)
                {
                    requestStream.WriteString("--");
                    contentLength += 2;
                }
            }
            httpWebRequest.ContentLength = contentLength;
        }


        public HttpWebRequest PrepareRequest()
        {
            httpWebRequest = (HttpWebRequest) WebRequest.Create(Uri);

            SetupHeader();

            SetupBody();

            return httpWebRequest;
        }

        void SetupClientCertificates()
        {
            if (ClientCertificates == null || ClientCertificates.Count == 0)
                return;

            httpWebRequest.ClientCertificates.AddRange(ClientCertificates);
        }

        void SetupAuthentication()
        {
            SetupClientCertificates();

            if (_forceBasicAuth)
            {
                string authInfo = _username + ":" + _password;
                authInfo = Convert.ToBase64String(Encoding.Default.GetBytes(authInfo));
                httpWebRequest.Headers["Authorization"] = "Basic " + authInfo;
            }
            else
            {
                var networkCredential = new NetworkCredential(_username, _password);
                httpWebRequest.Credentials = networkCredential;
            }
        }


        public void SetCacheControlToNoCache()
        {
            _cachePolicy = new HttpRequestCachePolicy(HttpRequestCacheLevel.NoCacheNoStore);
        }

        public void SetCacheControlWithMaxAge(TimeSpan maxAge)
        {
            _cachePolicy = new HttpRequestCachePolicy(HttpCacheAgeControl.MaxAge, maxAge);
        }

        public void SetCacheControlWithMaxAgeAndMaxStale(TimeSpan maxAge, TimeSpan maxStale)
        {
            _cachePolicy = new HttpRequestCachePolicy(HttpCacheAgeControl.MaxAgeAndMaxStale, maxAge, maxStale);
        }

        public void SetCacheControlWithMaxAgeAndMinFresh(TimeSpan maxAge, TimeSpan minFresh)
        {
            _cachePolicy = new HttpRequestCachePolicy(HttpCacheAgeControl.MaxAgeAndMinFresh, minFresh);
        }
    }
}