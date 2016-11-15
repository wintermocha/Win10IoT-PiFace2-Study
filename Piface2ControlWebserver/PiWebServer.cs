using System;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using Windows.Data.Json;
using Windows.Media.Protection.PlayReady;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;
using Windows.UI.Composition;
using Piface2ControlCore;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace Piface2ControlWebserver
{
    public sealed class PiWebServer
    {

        private StreamSocketListener listener; // the socket listner to listen for TCP requests
                                               // Note: this has to stay in scope!

        private const uint BufferSize = 8192; // this is the max size of the buffer in bytes 
        public PiWebServer()
        {

        }

        public void Initialise()
        {
            listener = new StreamSocketListener();

            listener.BindServiceNameAsync("1111");

            listener.ConnectionReceived += async (sender, args) =>
            {
                // call the handle request function when a request comes in
                HandleRequest(sender, args);
            };
        }

        public async void HandleRequest(StreamSocketListener sender, StreamSocketListenerConnectionReceivedEventArgs args)
        {
            StringBuilder request = new StringBuilder();

            // Handle a incoming request
            // First read the request
            using (IInputStream input = args.Socket.InputStream)
            {
                byte[] data = new byte[BufferSize];
                IBuffer buffer = data.AsBuffer();
                uint dataRead = BufferSize;
                while (dataRead == BufferSize)
                {
                    await input.ReadAsync(buffer, BufferSize, InputStreamOptions.Partial);
                    request.Append(Encoding.UTF8.GetString(data, 0, data.Length));
                    dataRead = buffer.Length;
                }
            }

            var query = GetQuery(request);
            string resultText = HandleQuery(query);

            // Send a response back
            using (IOutputStream output = args.Socket.OutputStream)
            {
                using (Stream response = output.AsStreamForWrite())
                {
                    // For now we are just going to reply to anything with Hello World!
                    byte[] bodyArray = Encoding.UTF8.GetBytes(resultText);

                    var bodyStream = new MemoryStream(bodyArray);

                    // This is a standard HTTP header so the client browser knows the bytes returned are a valid http response
                    var header = "HTTP/1.1 200 OK\r\n" +
                                $"Content-Length: {bodyStream.Length}\r\n" +
                                    "Connection: close\r\n\r\n";

                    byte[] headerArray = Encoding.UTF8.GetBytes(header);

                    // send the header with the body inclded to the client
                    await response.WriteAsync(headerArray, 0, headerArray.Length);
                    await bodyStream.CopyToAsync(response);
                    await response.FlushAsync();
                }
            }
        }

        /// <summary>
        /// 호출된 url의 쿼리 처리
        /// </summary>
        /// <param name="query">string 배열 [0] : 처리 유형 [1]이후 : 유형별 필요 parameter</param> 
        /// <returns></returns>
        private string HandleQuery(string[] query)
        {
            string resultText = string.Empty;

            switch (query[0])
            {
                case "GetPiFaceStatus":
                    resultText = GetPiFaceStatus();
                    break;
                case "ControlPiFaceLed":
                    resultText = ControlPiFaceLed(query);
                    break;
                default:
                    resultText = "XXXXX";
                    break;
            }

            return resultText;
        }

        private string ControlPiFaceLed(string[] query)
        {
            int targetLedNo;
            string resultString = "XXXXXXX";
            if (int.TryParse(query[1], out targetLedNo) == true && targetLedNo < 8)
            {
                MCP23S17.WritePin(PiFaceDigital2.LedAdress[targetLedNo],
                    this.CheckLedStatus(PiFaceDigital2.LedAdress[targetLedNo]) == MCP23S17.On
                        ? MCP23S17.Off
                        : MCP23S17.On);
                //MCP23S17.WritePin(PiFaceDigital2.LedAdress[targetLedNo], Convert.ToByte(query[1]));

                resultString = "ok";
            }
            return resultString;
        }

        private string GetPiFaceStatus()
        {
            UInt16 inputs = MCP23S17.ReadRegister16();

            JObject status = new JObject(
                new JProperty("LED",
                    new JArray(
                        ((inputs & 1 << PiFaceDigital2.LED0) != 0) ? "on" : "off",
                        ((inputs & 1 << PiFaceDigital2.LED1) != 0) ? "on" : "off",
                        ((inputs & 1 << PiFaceDigital2.LED2) != 0) ? "on" : "off",
                        ((inputs & 1 << PiFaceDigital2.LED3) != 0) ? "on" : "off",
                        ((inputs & 1 << PiFaceDigital2.LED4) != 0) ? "on" : "off",
                        ((inputs & 1 << PiFaceDigital2.LED5) != 0) ? "on" : "off",
                        ((inputs & 1 << PiFaceDigital2.LED6) != 0) ? "on" : "off",
                        ((inputs & 1 << PiFaceDigital2.LED7) != 0) ? "on" : "off"
                        )),
                new JProperty("Button",
                    new JArray(
                        ((inputs & 1 << PiFaceDigital2.IN0) != 0) ? "on" : "off",
                        ((inputs & 1 << PiFaceDigital2.IN1) != 0) ? "on" : "off",
                        ((inputs & 1 << PiFaceDigital2.IN2) != 0) ? "on" : "off",
                        ((inputs & 1 << PiFaceDigital2.IN3) != 0) ? "on" : "off",
                        ((inputs & 1 << PiFaceDigital2.IN4) != 0) ? "on" : "off",
                        ((inputs & 1 << PiFaceDigital2.IN5) != 0) ? "on" : "off",
                        ((inputs & 1 << PiFaceDigital2.IN6) != 0) ? "on" : "off",
                        ((inputs & 1 << PiFaceDigital2.IN7) != 0) ? "on" : "off"
                        )));

            return status.ToString();
        }

        private string[] GetQuery(StringBuilder request)
        {
            var requestLines = request.ToString().Split(' ');

            var url = requestLines.Length > 1
                              ? requestLines[1] : string.Empty;

            var uri = new Uri("http://localhost" + url);
            var query = uri.AbsolutePath.Split('/');
            return query;
        }

        private byte CheckLedStatus(byte led)
        {
            UInt16 Inputs = MCP23S17.ReadRegister16();

            return ((Inputs & 1 << led) != 0) ? MCP23S17.On : MCP23S17.Off;
        }

    }


}