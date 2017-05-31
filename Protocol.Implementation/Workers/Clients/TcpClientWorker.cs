﻿namespace FlowProtocol.Implementation.Workers.Clients
{
    using System;
    using System.Diagnostics;
    using System.Linq;
    using System.Net;
    using System.Net.Sockets;
    using Interfaces.CommonConventions;
    using Interfaces.Response;
    using Interfaces.Workers;
    using ProtocolHelpers;
    using Results;
    using static Interfaces.CommonConventions.Conventions;

    public class TcpClientWorker : IFlowClientWorker
    {
        //private const string AuthenticationFormat =
        //    @"AUTH --clienttype='tcp' --listenport='0' --login='{0}' --pass='{1}'";

        private const string clientSaysDoNotTranslate = "Do Not Translate";

        private readonly IFlowProtocolResponseParser _parser;

        private readonly string AuthenticationTemplate =
            @"AUTH  --login='{0}' --pass='{1}'";

        private readonly string RegisterTemplate =
            @"REGISTER  --login='{0}' --pass='{1}' --name='{2}'";

        private readonly string TranslateTemplate =
            @"TRANSLATE  --sourcetext='{0}' --sourcelang='{1}' --targetlang='{2}'";

        private readonly string SendMessageTemplate =
            @"SENDMSG --to='{0}' --msg='{1}' --sourcelang='{2}' --sessiontoken='{3}'";

        private readonly string GetMessageUnmodifiedTemplate =
            @"GETMSG --sessiontoken='{0}' --donottranslate";

        private readonly string GetMessageTranslatedTemplate =
            @"GETMSG --sessiontoken='{0}' --translateto='{1}'";

        private TcpClient _client;
        private bool _initialized;
        private string _login;
        private string _password;

        private Guid _sessionToken = Guid.Empty;

        public int Port { get; private set; }
        public IPAddress RemoteHostIpAddress { get; private set; }

        #region CONSTRUCTORS

        public TcpClientWorker(IFlowProtocolResponseParser parser)
        {
            _parser = parser;
        }

        #endregion

        public bool Connect(IPAddress ipAddress, int port)
        {
            RemoteHostIpAddress = ipAddress;
            Port = port;

            _initialized = true;

            try
            {
                _client = new TcpClient();
                _client.Connect(RemoteHostIpAddress, Port);

                NetworkStream networkStream = _client.GetStream();

                byte[] buffer = Commands.Hello.ToFlowProtocolAsciiEncodedBytesArray();


                if (networkStream.CanWrite)
                {
                    networkStream.Write(buffer, FromBeginning, buffer.Length);
                }

                string response = string.Empty;

                if (networkStream.CanRead)
                {
                    buffer = new byte[EthernetTcpUdpPacketSize];
                    int bytesRead = networkStream.Read(buffer, FromBeginning, EthernetTcpUdpPacketSize);
                    response = buffer.Take(bytesRead).ToArray().ToFlowProtocolAsciiDecodedString();
                }

                var responseComponents = _parser.ParseResponse(response);

                if (responseComponents.TryGetValue(Cmd, out string cmd))
                {
                    if (cmd.Equals(Commands.Hello))
                    {
                        return true;
                    }
                }
            }
            catch (Exception exception)
            {
                Debug.WriteLine(exception);
            }
            finally
            {
                _client.Close();
            }
            return false;
        }

        public bool Authenticate(string login, string password)
        {
            if (_initialized == false)
            {
                throw new Exception("No connection to server");
            }

            _login = login;
            _password = password;

            _client = new TcpClient();

            try
            {
                _client.Connect(RemoteHostIpAddress, Port);

                NetworkStream networkStream = _client.GetStream();

                string textToBeSent = string.Format(AuthenticationTemplate, login, password);

                byte[] buffer = textToBeSent.ToFlowProtocolAsciiEncodedBytesArray();

                if (networkStream.CanWrite)
                {
                    networkStream.Write(buffer, FromBeginning, buffer.Length);
                }

                string response = string.Empty;

                if (networkStream.CanRead)
                {
                    buffer = new byte[EthernetTcpUdpPacketSize];
                    int bytesRead = networkStream.Read(buffer, FromBeginning, EthernetTcpUdpPacketSize);
                    response = buffer.Take(bytesRead).ToArray().ToFlowProtocolAsciiDecodedString();
                }

                var responseComponents = _parser.ParseResponse(response);

                if (responseComponents.TryGetValue(Cmd, out string cmd))
                {
                    if (cmd == Commands.Auth)
                    {
                        if (responseComponents.TryGetValue(StatusDescription, out string statusDesc))
                        {
                            if (statusDesc == Error)
                            {
                                return false;
                            }
                            if (responseComponents.TryGetValue(SessionToken, out string token))
                            {
                                _sessionToken = Guid.Parse(token);
                                return true;
                            }
                        }
                    }
                }
            }
            catch (Exception exception)
            {
                Debug.WriteLine(exception);
            }
            finally
            {
                _client.Close();
            }
            return false;
        }

        public bool Register(string login, string password, string name)
        {
            if (_initialized == false)
            {
                throw new Exception("No connection to server");
            }

            _client = new TcpClient();

            try
            {
                _client.Connect(RemoteHostIpAddress, Port);

                NetworkStream networkStream = _client.GetStream();

                string textToBeSent = string.Format(RegisterTemplate, login, password, name);

                byte[] buffer = textToBeSent.ToFlowProtocolAsciiEncodedBytesArray();

                if (networkStream.CanWrite)
                {
                    networkStream.Write(buffer, FromBeginning, buffer.Length);
                }

                string response = string.Empty;

                if (networkStream.CanRead)
                {
                    buffer = new byte[EthernetTcpUdpPacketSize];
                    int bytesRead = networkStream.Read(buffer, FromBeginning, EthernetTcpUdpPacketSize);
                    response = buffer.Take(bytesRead).ToArray().ToFlowProtocolAsciiDecodedString();
                }

                var responseComponents = _parser.ParseResponse(response);

                if (responseComponents.TryGetValue(Cmd, out string cmd))
                {
                    if (cmd == Commands.Register)
                    {
                        if (responseComponents.TryGetValue(StatusDescription, out string statusDesc))
                        {
                            if (statusDesc == Error)
                            {
                                return false;
                            }
                            if (statusDesc == Ok)
                            {
                                return true;
                            }
                        }
                    }
                }
            }
            catch (Exception exception)
            {
                Debug.WriteLine(exception);
            }
            finally
            {
                _client.Close();
            }
            return false;
        }

        public string Translate(string sourceText, string sourceTextLang, string targetTextLanguage)
        {
            if (_initialized == false)
            {
                throw new Exception("No connection to server");
            }

            _client = new TcpClient();

            try
            {
                _client.Connect(RemoteHostIpAddress, Port);

                NetworkStream networkStream = _client.GetStream();

                // From "English" to "en", from "Romanian" to "ro", etc.
                ConvertToFlowProtocolLanguageNotations(ref sourceTextLang);
                ConvertToFlowProtocolLanguageNotations(ref targetTextLanguage);

                string textToBeSent = string.Format(TranslateTemplate,
                    sourceText, sourceTextLang, targetTextLanguage);

                byte[] buffer = textToBeSent.ToFlowProtocolAsciiEncodedBytesArray();

                if (networkStream.CanWrite)
                {
                    networkStream.Write(buffer, FromBeginning, buffer.Length);
                }

                string response = string.Empty;

                if (networkStream.CanRead)
                {
                    buffer = new byte[EthernetTcpUdpPacketSize];
                    int bytesRead = networkStream.Read(buffer, FromBeginning, EthernetTcpUdpPacketSize);
                    response = buffer.Take(bytesRead).ToArray().ToFlowProtocolAsciiDecodedString();
                }

                var responseComponents = _parser.ParseResponse(response);

                if (responseComponents.TryGetValue(Cmd, out string cmd))
                {
                    if (cmd == Commands.Translate)
                    {
                        if (responseComponents.TryGetValue(ResultValue, out string resultValue))
                        {
                            return resultValue;
                        }
                    }
                }
            }
            catch (Exception exception)
            {
                Debug.WriteLine(exception);
            }
            finally
            {
                _client.Close();
            }
            return string.Empty;
        }

        public SendMessageResult SendMessage(string recipient, string messageText, string messageTextLang)
        {
            if (_initialized == false)
            {
                throw new Exception("No connection to server");
            }

            if (_sessionToken == Guid.Empty)
            {
                throw new Exception("Not authorized. You have to sign in first.");
            }

            _client = new TcpClient();

            try
            {
                _client.Connect(RemoteHostIpAddress, Port);

                NetworkStream networkStream = _client.GetStream();

                // From "English" to "en", from "Romanian" to "ro", etc.
                ConvertToFlowProtocolLanguageNotations(ref messageTextLang);

                string textToBeSent = string.Format(SendMessageTemplate,
                    recipient, messageText, messageTextLang, _sessionToken);

                byte[] buffer = textToBeSent.ToFlowProtocolAsciiEncodedBytesArray();

                if (networkStream.CanWrite)
                {
                    networkStream.Write(buffer, FromBeginning, buffer.Length);
                }

                string response = string.Empty;

                if (networkStream.CanRead)
                {
                    buffer = new byte[EthernetTcpUdpPacketSize];
                    int bytesRead = networkStream.Read(buffer, FromBeginning, EthernetTcpUdpPacketSize);
                    response = buffer.Take(bytesRead).ToArray().ToFlowProtocolAsciiDecodedString();
                }

                var responseComponents = _parser.ParseResponse(response);

                if (responseComponents.TryGetValue(Cmd, out string cmd))
                {
                    if (cmd == Commands.SendMessage)
                    {
                        if (responseComponents.TryGetValue(StatusDescription, out string statusDesc))
                        {
                            if (statusDesc == Error)
                            {
                                return new SendMessageResult
                                {
                                    Success = false
                                };
                            }
                            if (statusDesc == Ok)
                            {
                                if (responseComponents.TryGetValue(ResultValue, out string resultValue))
                                {
                                    return new SendMessageResult
                                    {
                                        Success = true,
                                        ResponseMessage = resultValue
                                    };
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception exception)
            {
                Debug.WriteLine(exception);
            }
            finally
            {
                _client.Close();
            }
            return new SendMessageResult
            {
                Success = false
            };
        }

        public GetMessageResult GetMessage(string translationMode)
        {
            if (_initialized == false)
            {
                throw new Exception("No connection to server");
            }

            if (_sessionToken == Guid.Empty)
            {
                throw new Exception("Not authorized. You have to sign in first.");
            }

            _client = new TcpClient();

            try
            {
                _client.Connect(RemoteHostIpAddress, Port);

                NetworkStream networkStream = _client.GetStream();

                if (translationMode == clientSaysDoNotTranslate)
                {
                    string textToBeSent = string.Format(GetMessageUnmodifiedTemplate,
                        _sessionToken);

                    byte[] buffer = textToBeSent.ToFlowProtocolAsciiEncodedBytesArray();

                    if (networkStream.CanWrite)
                    {
                        networkStream.Write(buffer, FromBeginning, buffer.Length);
                    }
                }
                else
                {
                    // From "English" to "en", from "Romanian" to "ro", etc.
                    ConvertToFlowProtocolLanguageNotations(ref translationMode);

                    string textToBeSent = string.Format(GetMessageTranslatedTemplate,
                        _sessionToken, translationMode);

                    byte[] buffer = textToBeSent.ToFlowProtocolAsciiEncodedBytesArray();

                    if (networkStream.CanWrite)
                    {
                        networkStream.Write(buffer, FromBeginning, buffer.Length);
                    }
                }

                string response = string.Empty;

                if (networkStream.CanRead)
                {
                    byte[] buffer = new byte[EthernetTcpUdpPacketSize];
                    int bytesRead = networkStream.Read(buffer, FromBeginning, EthernetTcpUdpPacketSize);
                    response = buffer.Take(bytesRead).ToArray().ToFlowProtocolAsciiDecodedString();
                }

                var responseComponents = _parser.ParseResponse(response);

                if (responseComponents.TryGetValue(Cmd, out string cmd))
                {
                    if (cmd == Commands.GetMessage)
                    {
                        if (responseComponents.TryGetValue(StatusDescription, out string statusDesc))
                        {
                            if (statusDesc == Error)
                            {
                                return new GetMessageResult
                                {
                                    Success = false
                                };
                            }
                            if (statusDesc == Ok)
                            {
                                responseComponents.TryGetValue(SenderId, out string senderId);
                                responseComponents.TryGetValue(SenderName, out string senderName);
                                responseComponents.TryGetValue(Message, out string message);

                                return new GetMessageResult
                                {
                                    Success = true,
                                    SenderId = senderId,
                                    SenderName = senderName,
                                    MessageBody = message
                                };
                            }
                        }
                    }
                }
            }
            catch (Exception exception)
            {
                Debug.WriteLine(exception);
            }
            finally
            {
                _client.Close();
            }
            return new GetMessageResult
            {
                Success = false
            };
        }


        public void Dispose()
        {
            ((IDisposable) _client)?.Dispose();
        }

        public void ConvertToFlowProtocolLanguageNotations(ref string textLanguage)
        {
            const string english = "English";
            const string romanian = "Romanian";
            const string russian = "Russian";
            const string autoDetection = "Auto Detection";

            const string ro = "ro";
            const string ru = "ru";
            const string en = "en";
            const string unknown = "unknown";

            switch (textLanguage)
            {
                case english:
                    textLanguage = en;
                    break;
                case romanian:
                    textLanguage = ro;
                    break;
                case russian:
                    textLanguage = ru;
                    break;
                case autoDetection:
                    textLanguage = unknown;
                    break;
            }
        }
    }
}