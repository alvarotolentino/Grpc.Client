using System;
using System.IO;
using Google.Protobuf;
using Grpctest.Messages;
using Grpc.Core;
using static Grpctest.Messages.UserService;
using System.Threading.Tasks;
using System.Threading;
using System.Collections.Generic;

namespace Grpc.Client
{
    class Program
    {
        static void Main(string[] args)
        {
            const int port = 9000;
            var cacert = File.ReadAllText("Keys/ca.crt");
            var cert = File.ReadAllText("Keys/client.crt");
            var key = File.ReadAllText("Keys/client.key");

            var keypair = new KeyCertificatePair(cert, key);
            var sslCerts = new SslCredentials(cacert, keypair);

            var channel = new Channel("%COMPUTER_NAME%", port, sslCerts);
            var client = new UserServiceClient(channel);

            System.Console.WriteLine("----------UNARY CALL----------");
            GetByUserIdAsync(client).Wait();
            System.Console.WriteLine("------------------------------");
            System.Console.WriteLine("-----SERVER STREAMING CALL----");
            GetAllAsync(client).Wait();
            System.Console.WriteLine("------------------------------");
            System.Console.WriteLine("-----CLIENT STREAMING CALL----");
            AddImageAsync(client).Wait();
            System.Console.WriteLine("------------------------------");
            System.Console.WriteLine("-BIDIRECTIONAL STREAMING CALL-");
            SaveAllAsync(client).Wait();
            System.Console.WriteLine("------------------------------");
        }

        private static async Task GetByUserIdAsync(UserServiceClient client)
        {
            var md = new Metadata();
            md.Add("UserName", "A");
            md.Add("password", "12345");

            var res = await client.GetByUserIdAsync(new GetByUserIdRequest()
            {
                UserId = 1
            }, md);

            System.Console.WriteLine(res.User);
        }


        private static async Task GetAllAsync(UserServiceClient client)
        {
            using (var call = client.GetAll(new GetAllRequest()))
            {
                var resStream = call.ResponseStream;
                while (await resStream.MoveNext(CancellationToken.None))
                {
                    System.Console.WriteLine(resStream.Current.User);
                }
            }
        }


        private static async Task AddImageAsync(UserServiceClient client)
        {
            var md = new Metadata();
            md.Add("UserId", "1");

            FileStream fs = File.OpenRead("Images/Test.jpg");

            using (var call = client.AddImage(md))
            {
                var stream = call.RequestStream;

                while (true)
                {
                    byte[] buffer = new byte[64 * 1024];
                    int numRead = await fs.ReadAsync(buffer, 0, buffer.Length);

                    if (numRead == 0)
                        break;

                    if (numRead < buffer.Length)
                        Array.Resize(ref buffer, numRead);

                    await stream.WriteAsync(new AddImageRequest()
                    {
                        Data = ByteString.CopyFrom(buffer)
                    });
                }

                await stream.CompleteAsync();

                var res = await call.ResponseAsync;
                System.Console.WriteLine(res.IsOk);
            }
        }

        private static async Task SaveAllAsync(UserServiceClient client)
        {
            var u1 = new User
            {
                Id = 4,
                FirstName = "K",
                LastName = "V",
                Birthday = new DateTime(1984, 7, 26).Ticks
            };

            u1.Vehicles.Add(new Vehicle { Id = 7, RegNumber = "XI" });
            u1.Vehicles.Add(new Vehicle { Id = 8, RegNumber = "ZI" });

            var users = new List<User> { u1 };

            using (var call = client.SaveAll())
            {
                var reqStream = call.RequestStream;
                var resStream = call.ResponseStream;

                var resTask = Task.Run(async () =>
                {
                    while (await resStream.MoveNext(CancellationToken.None))
                    {
                        System.Console.WriteLine($"Saved: {resStream.Current.User}");
                    }
                });

                foreach (var u in users)
                {
                    await reqStream.WriteAsync(new UserRequest{
                        User = u
                    });
                }

                await call.RequestStream.CompleteAsync();
                await resTask;
            }
        }
    }
}
