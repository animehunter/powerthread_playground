using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Wintellect.Threading.AsyncProgModel;
using System.Net.Sockets;
using System.IO;

namespace powerthread_playground
{
    class Program
    {
        class ClientState
        {
            public readonly TcpClient tcp;

            public ClientState(TcpClient tcp)
            {
                this.tcp = tcp;
            }
        }

        static IEnumerator<Int32> HandleClient(AsyncEnumerator ae, IDictionary<TcpClient, ClientState> clients, ClientState client)
        {
            TcpClient tcp = client.tcp;
            Socket sock = tcp.Client;

            byte[] buffer = new byte[1024];
            int len = 0;

            tcp.SendTimeout = 10000;
            tcp.ReceiveTimeout = 10000;
            tcp.NoDelay = true;
            tcp.LingerState.Enabled = false;
            while (true)
            {
                // read message
                sock.BeginReceive(buffer, 0, buffer.Length, SocketFlags.None, ae.End(), null);
                yield return 1;

                try
                {
                    len = sock.EndReceive(ae.DequeueAsyncResult());
                    if (len == 0) break;
                }
                catch (Exception)
                {
                    break;
                }

                // echo the data back to the client
                sock.BeginSend(buffer, 0, len, SocketFlags.None, ae.End(), null);
                yield return 1;

                try
                {
                    sock.EndSend(ae.DequeueAsyncResult());
                }
                catch (Exception)
                {
                    break;
                }
            }

            var removed = false;
            lock(clients)
            {
                removed = clients.Remove(tcp);
            }
            Console.WriteLine("Client disconnected {0} - {1}", sock.RemoteEndPoint, removed);
        }

        static IEnumerator<Int32> Server(AsyncEnumerator ae)
        {
            var clients = new Dictionary<TcpClient, ClientState>();
            var serverSock = new TcpListener(System.Net.IPAddress.Parse("0.0.0.0"), 10000);
            serverSock.Start();

            while(true)
            {
                serverSock.BeginAcceptTcpClient(ae.End(), null);
                yield return 1;

                try 
                {
                    TcpClient client = serverSock.EndAcceptTcpClient(ae.DequeueAsyncResult());
                    // handle client
                    var newClientState = new ClientState(client);
                    lock (clients)
                    {
                        clients[client] = newClientState;
                    }
                    var clientAe = new AsyncEnumerator();
                    clientAe.BeginExecute(HandleClient(clientAe, clients, newClientState), clientAe.EndExecute, clientAe);
                    
                    Console.WriteLine("Client Connected {0}", client.Client.RemoteEndPoint);
                }
                catch(SocketException)
                {
                    Console.WriteLine("A client failed to connect");
                }
            }
        }

        static void Main(string[] args)
        {
            var ae = new AsyncEnumerator();
            var r = ae.BeginExecute(Server(ae), ae.EndExecute);
            r.AsyncWaitHandle.WaitOne();
        }
    }
}
