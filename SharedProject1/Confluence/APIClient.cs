/**
 * TestRail API binding for .NET (API v2, available since TestRail 3.0)
 * Compatible with TestRail 3.0 and later
 *
 * Learn more:
 *
 * http://docs.gurock.com/testrail-api2/start
 * http://docs.gurock.com/testrail-api2/accessing
 *
 * Copyright Gurock Software GmbH. See license.md for details.
 */

using System;
using System.Net;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace SharedProject.Confluence
{
    public class APIClient
    {
        private string m_user;
        private string m_password;
        private string m_url;

        public APIClient(string base_url)
        {
            if (!base_url.EndsWith("/"))
            {
                base_url += "/";
            }

            this.m_url = base_url + "wiki/rest/api/content/";

            ServicePointManager.Expect100Continue = true;
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
        }

        /**
         * Get/Set User
         *
         * Returns/sets the user used for authenticating the API requests.
         */
        public string User
        {
            get { return this.m_user; }
            set { this.m_user = value; }
        }

        /**
         * Get/Set Password
         *
         * Returns/sets the password used for authenticating the API requests.
         */
        public string Password
        {
            get { return this.m_password; }
            set { this.m_password = value; }
        }

        /**
         * Send Get
         *
         * Issues a GET request (read) against the API and returns the result
         * (as JSON object, i.e. JObject or JArray instance).
         * If 'get_attachment/:attachment_id', returns a string
         *
         * Arguments:
         *
         * uri                  The API method to call including parameters
         *                      (e.g. get_case/1)
         */
        public object SendGet(string uri, object filepath = null)
        {
            return SendRequest("GET", uri, filepath);
        }

        /**
         * Send POST
         *
         * Issues a POST request (write) against the API and returns the result
         * (as JSON object, i.e. JObject or JArray instance).
         *
         * Arguments:
         *
         * uri                  The API method to call including parameters
         *                      (e.g. add_case/1)
         * data                 The data to submit as part of the request (as
         *                      serializable object, e.g. a dictionary)
         *                      If adding an attachment, must be the path
         *                      to the file
         */
        public object SendPost(string uri, object data)
        {
            return SendRequest("POST", uri, data);
        }

        public object SendPut(string uri, object data)
        {
            return SendRequest("PUT", uri, data);
        }

        private object SendRequest(string method, string uri, object data)
        {
            string url = this.m_url + uri;

            // Create the request object and set the required HTTP method
            // (GET/POST) and headers (content type and basic auth).
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            request.ContentType = "application/json";
            request.Method = method;
            request.Accept = "application/json";
            //request.UserAgent = "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_3)AppleWebKit/537.36 (KHTML, like Gecko) Chrome/81.0.4044.92 Safari/537.36";

            string auth = Convert.ToBase64String(
                Encoding.ASCII.GetBytes(
                    String.Format(
                        "{0}:{1}",
                        this.m_user,
                        this.m_password
                    )
                )
            );

            request.Headers.Add("Authorization", "Basic " + auth);

            if (method == "POST" || method == "PUT")
            {
                if (uri.StartsWith("add_attachment"))   // add_attachment API requests
                {
                    string boundary = String.Format("{0:N}", Guid.NewGuid());
                    string filePath = (String)data;

                    request.ContentType = "multipart/form-data; boundary=" + boundary;

                    using (MemoryStream postDataStream = new MemoryStream())
                    using (StreamWriter postDataWriter = new StreamWriter(postDataStream))
                    {
                        postDataWriter.Write("\r\n--" + boundary + "\r\n");
                        postDataWriter.Write("Content-Disposition: form-data; name=\"attachment\";"
                                        + "filename=\"{0}\""
                                        + "\r\nContent-Type: {1}\r\n\r\n",
                                        Path.GetFileName(filePath),
                                        Path.GetExtension(filePath));
                        postDataWriter.Flush();

                        using (FileStream fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                        {
                            byte[] buffer = new byte[1024];
                            int bytesRead;
                            while ((bytesRead = fileStream.Read(buffer, 0, buffer.Length)) != 0)
                            {
                                postDataStream.Write(buffer, 0, bytesRead);
                            }

                            postDataWriter.Write("\r\n--" + boundary + "--\r\n");
                            postDataWriter.Flush();


                            request.ContentLength = postDataStream.Length;

                            using (Stream requestStream = request.GetRequestStream())
                            {
                                postDataStream.WriteTo(requestStream);
                            }
                        }
                    }
                }
                // For non-attachment requests, add the POST arguments, if any. 
                // We just serialize the passed data object (i.e. a dictionary)
                // and then add it to the request body.
                else if (data != null)
                {
                    byte[] block = Encoding.UTF8.GetBytes(
                        JsonConvert.SerializeObject(data).Replace("contentappearancedraft", "content-appearance-draft").Replace("contentappearancepublished", "content-appearance-published")
                    );
                    request.GetRequestStream().Write(block, 0, block.Length);
                }
            }

            // Execute the actual web request (GET or POST) and record any
            // occurred errors.
            Exception ex = null;
            HttpWebResponse response = null;
            try
            {
                response = (HttpWebResponse)request.GetResponse();
            }
            catch (WebException e)
            {
                if (e.Response == null)
                {
                    throw;
                }

                response = (HttpWebResponse)e.Response;
                ex = e;
            }

            // If 'get_attachment' (but not 'get_attachments') returned valid status code, save the file
            if ((response != null) 
                && ((int)response.StatusCode == 200) 
                && (uri.StartsWith("get_attachment/")))
            {
                byte[] buffer = new byte[1024];

                try
                {
                    Stream receiveStream = response.GetResponseStream();
                    Stream fileStream = System.IO.File.Create((String)data);
                    long bytesReceived = 0;
                    int bytesRead;

                    while ((bytesRead = receiveStream.Read(buffer, 0, buffer.Length)) > 0)
                    { 
                        fileStream.Write(buffer, 0, bytesRead);
                        bytesReceived += bytesRead;
                    }
                    fileStream.Flush();
                    receiveStream.Close();
                    fileStream.Close();
                    response.Close();

                    return (String) data;
                }
                catch (Exception e)
                {
                    //throw new APIException("Unable to save attachment.");
                    int i = 0;
                }
            }

            // Read the response body, if any, and deserialize it from JSON.
            string text = "";
            if (response != null)
            {
                var reader = new StreamReader(
                    response.GetResponseStream(),
                    Encoding.UTF8
                );

                using (reader)
                {
                    text = reader.ReadToEnd();
                }
            }

            JContainer result = null;
            if (text != "")
            {
                if (text.StartsWith("["))
                {
                    result = JArray.Parse(text);
                }
                else
                {
                    try
                    {
                        result = JObject.Parse(text);
                    }
                    catch (Exception e)   // Response is not in JSON format
                    {
                        //throw new APIException(String.Format(
                        //    "TestRail API returned the following: {0}\n",
                        //    text));

                        int i = 0;
                    }
                }
            }
            else
            {
                result = new JObject();
            }
            /*
            // Check for any occurred errors and add additional details to
            // the exception message, if any (e.g. the error message returned
            // by TestRail).
            if (ex != null)
            {
                string error = (string)result["error"];
                if (error != null)
                {
                    error = '"' + error + '"';
                }
                else
                {
                    error = "No additional error message received";
                }

                throw new APIException(
                    String.Format(
                        "TestRail API returned HTTP {0} ({1})",
                        (int)response.StatusCode,
                        error
                    )
                );
            }
            */

            return result;
        }

        public string SeanPost(string uri, object data)
        {
            string url = this.m_url + uri;


            HttpWebResponse response = null;
            String responseString = "";

            // Enable UseUnsafeHeaderParsing
            //if (!ToggleAllowUnsafeHeaderParsing(true))
            //{
            //    // Couldn't set flag. Log the fact, throw an exception or whatever.
            //}

            try
            {
                HttpWebRequest request = (HttpWebRequest)HttpWebRequest.Create(url);
                request.Method = "POST";
                request.ContentType = "application/json";


                string authInfo = this.m_user + ":" + this.m_password;
                request.Headers["Authorization"] = "Basic " + Convert.ToBase64String(Encoding.ASCII.GetBytes(authInfo));

                // The following fixes a response code of 500 in the RMS Auth/Login endpoint
                request.Accept = "*/*";
                //                request.Accept = "text/html, application/xhtml+xml, */*";
                request.ServicePoint.Expect100Continue = false;

                request.Timeout = Convert.ToInt32(60 * 1000);
                request.ReadWriteTimeout = Convert.ToInt32(60 * 1000);

                //if (referer != null)

                //    request.Referer = referer;

                //if (headers != null)

                //    request.Headers.Add(headers);

                //if (cookies)
                //{
                //    if (current_cookies == null)

                //        current_cookies = new CookieContainer();    //create cookie container

                //    request.CookieContainer = current_cookies;      //set container for HttpWebRequest 
                //}

                using (StreamWriter writer = new StreamWriter(request.GetRequestStream()))
                {
                    writer.Write(data); //write your request payload
                }

                response = (HttpWebResponse)request.GetResponse();

                //foreach (Cookie cook in response.Cookies)
                //{
                //    Console.WriteLine("Domain: {0}, Name: {1}, value: {2}", cook.Domain, cook.Name, cook.Value);
                //}


                try
                {
                    responseString = new StreamReader(response.GetResponseStream()).ReadToEnd();
                }
                catch (Exception)
                {
                }



            }
            catch (WebException)
            {
                // return (HttpWebResponse)wex.Response;
            }

            return responseString;


        }


    }
}
