using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading.Tasks;

namespace b2cs
{
    public class B2api
    {
        static string API_AUTH_HOST = "https://api.backblaze.com";
        static string API_URL_BASE = "/b2api/v1/";

        public B2api(string account, string apiKey)
        {
            m_accountId = account;
            m_apiKey = apiKey;
        }

        private string m_accountId;
        private string m_apiKey;

        private bool m_authorized = false;
        private string m_authToken;
        private string m_apiUrl;

        public void EnsureAuthorized()
        {
            if (m_authorized)
                return;

            AuthorizeResponse authResponse = DoAuthorize();
            m_authToken = authResponse.AuthorizationToken;
            m_apiUrl = authResponse.ApiUrl + API_URL_BASE;
        }

        private T ParseResponse<T>(WebResponse response)
        {
            var deserializer = new DataContractJsonSerializer(typeof(T));
            using (var stream = response.GetResponseStream())
            {
                return (T)deserializer.ReadObject(stream);
            }
        }

        private TResponse MakeRequest<TResponse>(string apiOperation, ExpandoObject requestBody)
        {
            var request = WebRequest.Create(m_apiUrl + apiOperation);
            request.Method = "POST";
            request.Headers.Add(HttpRequestHeader.Authorization, m_authToken);
            request.ContentType = "text/json; charset=utf-8";

            if (requestBody != null)
            {
                using (var stream = request.GetRequestStream())
                {
                    string json = JsonFormat.Format(requestBody);
                    var bytes = Encoding.UTF8.GetBytes(json);
                    stream.Write(bytes, 0, bytes.Length);
                }
            }

            try
            {
                var response = request.GetResponse();
                return ParseResponse<TResponse>(response);
            }
            catch (WebException ex)
            {
                WebResponse response = ex.Response;
                using (var stream = response.GetResponseStream())
                {
                    var reader = new StreamReader(stream);
                    string responseText = reader.ReadToEnd();
                    Console.WriteLine(responseText);
                    throw;
                }
            }
        }

        [DataContract]
        public struct AuthorizeResponse
        {
            [DataMember(Name = "accountId")]
            public string AccountID { get; set; }

            [DataMember(Name = "authorizationToken")]
            public string AuthorizationToken { get; set; }

            [DataMember(Name = "apiUrl")]
            public string ApiUrl { get; set; }

            [DataMember(Name = "downloadUrl")]
            public string DownloadUrl { get; set; }
        }

        private AuthorizeResponse DoAuthorize()
        {
            string authorization = "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes(m_accountId + ":" + m_apiKey));

            var req = WebRequest.Create(API_AUTH_HOST + API_URL_BASE + "b2_authorize_account");
            req.Headers.Add(HttpRequestHeader.Authorization, authorization);
            using (WebResponse response = req.GetResponse())
            {
                return ParseResponse<AuthorizeResponse>(response);
            }
        }

        [DataContract]
        public struct ListBucketResponse
        {
            [DataContract]
            public struct BucketResponse
            {
                [DataMember(Name = "accountId")]
                public string AccountId { get; set; }

                [DataMember(Name = "bucketId")]
                public string BucketId { get; set; }

                [DataMember(Name = "bucketName")]
                public string BucketName { get; set; }

                [DataMember(Name = "bucketType")]
                public string BucketType { get; set; }
            }

            [DataMember(Name = "buckets")]
            public BucketResponse[] Buckets { get; set; }
        }

        public ListBucketResponse ListBuckets()
        {
            EnsureAuthorized();

            dynamic requestBody = new ExpandoObject();
            requestBody.accountId = m_accountId;

            var response = MakeRequest<ListBucketResponse>("b2_list_buckets", (ExpandoObject)requestBody);
            return response;
        }

        public struct ListFilesResponse
        {
            [DataContract]
            public struct FileResponse
            {
                [DataMember(Name = "fileId")]
                public string FileId { get; set; }

                [DataMember(Name = "fileName")]
                public string FileName { get; set; }

                [DataMember(Name = "action")]
                public string Action { get; set; }

                [DataMember(Name = "size")]
                public string Size { get; set; }

                [DataMember(Name = "uploadTimestamp")]
                public string UploadTimestamp { get; set; }
            }

            public List<FileResponse> Files { get; set; }
        }

        [DataContract]
        internal struct ListFilesResponseInternal
        {
            [DataMember(Name = "files")]
            public ListFilesResponse.FileResponse[] Files { get; set; }

            [DataMember(Name = "nextFileName")]
            public string NextFileName { get; set; }
        }

        public ListFilesResponse ListFiles(string bucketId)
        {
            dynamic requestBody = new ExpandoObject();
            requestBody.bucketId = bucketId;
            requestBody.maxFileCount = 1000; // this is the maximum allowed

            var ret = new ListFilesResponse();
            ret.Files = new List<ListFilesResponse.FileResponse>();

            ListFilesResponseInternal response;
            do
            {
                response = MakeRequest<ListFilesResponseInternal>("b2_list_file_names", (ExpandoObject)requestBody);
                ret.Files.AddRange(response.Files);
                requestBody.startFileName = response.NextFileName;
            }
            while (response.NextFileName != null);

            return ret;
        }

        [DataContract]
        public struct UploadUrlResponse
        {
            [DataMember(Name = "bucketId")]
            public string BucketId { get; set; }

            [DataMember(Name = "uploadUrl")]
            public string UploadUrl { get; set; }

            [DataMember(Name = "authorizationToken")]
            public string AuthorizationToken { get; set; }
        }

        [DataContract]
        public struct UploadResponse
        {
            [DataMember(Name = "fileId")]
            public string FileId { get; set; }

            [DataMember(Name = "fileName")]
            public string FileName { get; set; }

            [DataMember(Name = "accountId")]
            public string AccountId { get; set; }

            [DataMember(Name = "bucketId")]
            public string BucketId { get; set; }

            [DataMember(Name = "contentLength")]
            public string ContentLength { get; set; }

            [DataMember(Name = "contentSha1")]
            public string ContentSha1 { get; set; }

            [DataMember(Name = "fileInfo")]
            public string FileInfoJson { get; set; }
        }

        public UploadResponse? Upload(string bucketId, string sha1, string filename, Action<long, long, TimeSpan> progressCallback = null)
        {
            using (var file = File.OpenRead(filename))
            {
                if (file.Length > 5000000000)
                {
                    Console.WriteLine("Error: B2 only accepts files up to 5000000000 (5 billion) bytes in length.");
                    return null;
                }

                dynamic requestBody = new ExpandoObject();
                requestBody.bucketId = bucketId;

                var uploadUrlResponse = MakeRequest<UploadUrlResponse>("b2_get_upload_url", (ExpandoObject)requestBody);

                var uploadRequest = WebRequest.Create(uploadUrlResponse.UploadUrl);
                uploadRequest.Method = "POST";
                uploadRequest.ContentType = "application/octet-stream";
                uploadRequest.ContentLength = file.Length;
                uploadRequest.Headers.Add(HttpRequestHeader.Authorization, uploadUrlResponse.AuthorizationToken);
                uploadRequest.Headers.Add("X-Bz-Content-Sha1", sha1);
                uploadRequest.Headers.Add("X-Bz-File-Name", filename);

                // Disable write buffering to prevent us from using potentially ridiculous amounts of memory.
                ((HttpWebRequest)uploadRequest).AllowWriteStreamBuffering = false;

                DateTime lastUpdateTime = DateTime.Now;
                byte[] buffer = new byte[16384];
                using (var stream = uploadRequest.GetRequestStream())
                {
                    if (progressCallback == null)
                    {
                        file.CopyTo(stream);
                    }
                    else
                    {
                        long position = 0;
                        while (true)
                        {
                            int nBytes = file.Read(buffer, 0, buffer.Length);
                            if (nBytes == 0)
                                break;

                            position += nBytes;

                            TimeSpan delta;
                            if ((delta = (DateTime.Now - lastUpdateTime)) > TimeSpan.FromSeconds(1))
                            {
                                progressCallback(position, file.Length, delta);
                                lastUpdateTime = DateTime.Now;
                            }

                            stream.Write(buffer, 0, nBytes);
                            if (nBytes < buffer.Length)
                                break;
                        }
                        progressCallback(position, file.Length, DateTime.Now - lastUpdateTime);
                    }
                }

                return ParseResponse<UploadResponse>(uploadRequest.GetResponse());
            }
        }
    }
}
