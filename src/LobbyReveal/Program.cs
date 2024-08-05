using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Ekko;
using Newtonsoft.Json;
using Spectre.Console;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using Mono.Cecil;
using Mono.Cecil.Cil;
using System.Security.Cryptography;
using Mono.Cecil.Rocks;
using System.Runtime.CompilerServices;


namespace LobbyReveal
{
    internal class Program
    {
        private static List<LobbyHandler> _handlers = new List<LobbyHandler>();
        private static bool _update = true;

        public async static Task Main(string[] args)
        {
            String randomString = RandomString(8);

            ModifyBytecode();

            Console.Title = randomString;
            var watcher = new LeagueClientWatcher();
            watcher.OnLeagueClient += (clientWatcher, client) =>
            {
                /*Console.WriteLine(client.Pid);*/
                var handler = new LobbyHandler(new LeagueApi(client.ClientAuthInfo.RiotClientAuthToken,
                    client.ClientAuthInfo.RiotClientPort));
                _handlers.Add(handler);
                handler.OnUpdate += (lobbyHandler, names) => { _update = true; };
                handler.Start();
                _update = true;
            };
            new Thread(async () => { await watcher.Observe(); })
            {
                IsBackground = true
            }.Start();

            new Thread(() => { Refresh(); })
            {
                IsBackground = true
            }.Start();


            while (true)
            {
                var input = Console.ReadKey(true);
                if (!int.TryParse(input.KeyChar.ToString(), out var i) || i > _handlers.Count || i < 1)
                {
                    Console.WriteLine("Invalid input.");
                    _update = true;
                    continue;
                }

                var region = _handlers[i - 1].GetRegion();

                var link =
                    $"https://www.op.gg/multisearch/{region ?? Region.PH}?summoners=" +
                    HttpUtility.UrlEncode($"{string.Join(",", _handlers[i - 1].GetSummoners())}");
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    Process.Start(link);
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    Process.Start("xdg-open", link);
                }
                else
                {
                    Process.Start("open", link);
                }
                _update = true;
            }
        }

        static string RandomString(int length)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            Random random = new Random();
            return new string(Enumerable.Repeat(chars, length)
              .Select(s => s[random.Next(s.Length)]).ToArray());
        }

        private static void Refresh()
        {
            while (true)
            {
                if (_update)
                {
                    Console.Clear();
                    AnsiConsole.Write(new Markup("[u][yellow]https://www.github.com/Xin1337/Reveal[/][/]")
                        .Centered());
                    AnsiConsole.Write(new Markup("[u][gray][b]v1.0.3 - LobbyReveal[/][/][/]").Centered());
                    Console.WriteLine();
                    Console.WriteLine();
                    ModifyBytecode();
                    for (int i = 0; i < _handlers.Count; i++)
                    {
                        var link =
                            $"https://www.op.gg/multisearch/{_handlers[i].GetRegion() ?? Region.PH}?summoners=" +
                            HttpUtility.UrlEncode($"{string.Join(",", _handlers[i].GetSummoners())}");

                        AnsiConsole.Write(
                            new Panel(new Text($"{string.Join("\n", _handlers[i].GetSummoners())}\n\n{link}")
                                    .LeftJustified())
                                .Expand()
                                .SquareBorder()
                                .Header($"[red]League {i + 1}[/]"));
                        Console.WriteLine();
                    }

                    Console.WriteLine();
                    Console.WriteLine();
                    AnsiConsole.Write(new Markup("[u][cyan][b]Type the number to open op.gg![/][/][/]")
                        .LeftJustified());
                    Console.WriteLine();
                    _update = false;
                }

                Thread.Sleep(2000);
            }
        }

        private static byte[] GenerateRandomAssemblyBytes()
        {
            AssemblyDefinition assembly = AssemblyDefinition.CreateAssembly(
                new AssemblyNameDefinition("DynamicAssembly", new Version(1, 0, 0, 0)),
                "DynamicAssembly",
                ModuleKind.Dll
            );
            TypeDefinition type = new TypeDefinition(
                "DynamicAssembly",
                "DynamicType",
                Mono.Cecil.TypeAttributes.Public | Mono.Cecil.TypeAttributes.Class
            );
            assembly.MainModule.Types.Add(type);
            MethodDefinition method = new MethodDefinition(
                "DynamicMethod",
                Mono.Cecil.MethodAttributes.Public | Mono.Cecil.MethodAttributes.Static,
                assembly.MainModule.ImportReference(typeof(void))
            );
            type.Methods.Add(method);
            ILProcessor il = method.Body.GetILProcessor();

            Random rand = new Random();
            int a = rand.Next(0, 10);
            int b = rand.Next(0, 10);

            int x = a;
            int y = b;

            il.Emit(Mono.Cecil.Cil.OpCodes.Ldc_I4, x);
            il.Emit(Mono.Cecil.Cil.OpCodes.Ldc_I4, y);
            il.Emit(Mono.Cecil.Cil.OpCodes.Add);
            il.Emit(Mono.Cecil.Cil.OpCodes.Ret);

            MemoryStream stream = new MemoryStream();
            assembly.Write(stream);
            return stream.ToArray();
        }

        private static void ModifyBytecode()
        {
            var assembly = AssemblyDefinition.ReadAssembly(Assembly.GetExecutingAssembly().Location);
            var type = assembly.MainModule.GetType("LobbyReveal.Program");
            if (type != null)
            {
                var method = type.Methods.First(m => m.Name == "Decrypt");
                var body = method.Body;

                byte[] randomBytes = GenerateRandomAssemblyBytes();
                AssemblyDefinition dynamicAssembly = AssemblyDefinition.ReadAssembly(new MemoryStream(randomBytes));
                MethodDefinition dynamicMethod = null;
                if (dynamicAssembly.MainModule.Types.Count > 0)
                {
                    dynamicMethod = dynamicAssembly.MainModule.Types[0].Methods.FirstOrDefault(m => m.Name == "DynamicMethod");
                }
                body.Instructions.Clear();
                if (dynamicMethod != null)
                {
                    foreach (var instr in dynamicMethod.Body.Instructions)
                    {
                        body.Instructions.Add(instr);
                    }
                }

                //Console.WriteLine("Before modification: " + BitConverter.ToString(randomBytes));
                short[] opcodeValues = method.Body.Instructions.Select(i => i.OpCode.Value).ToArray();
                byte[] opcodeBytes = new byte[opcodeValues.Length * 2];
                Buffer.BlockCopy(opcodeValues, 0, opcodeBytes, 0, opcodeBytes.Length);
                //Console.WriteLine("After modification: " + BitConverter.ToString(method.Body.Instructions.Select(i => BitConverter.GetBytes(i.OpCode.Value)).SelectMany(b => b).ToArray()));

                // Replace the original method with the new method
                byte[] dynamicMethodBytes = dynamicMethod?.Body?.Instructions?.Select(i => BitConverter.GetBytes(i.OpCode.Value)).SelectMany(b => b).ToArray();
                if (dynamicMethodBytes != null)
                {
                    RuntimeHelpers.PrepareMethod(MethodBase.GetCurrentMethod().MethodHandle);
                    Marshal.Copy(dynamicMethodBytes, 0, MethodBase.GetCurrentMethod().MethodHandle.GetFunctionPointer(), dynamicMethodBytes.Length);
                }
            }
        }


        private static string Decrypt(string input)
        {
            byte[] keyArray = Encoding.ASCII.GetBytes("0123456789abcdef");
            byte[] toDecryptArray = Convert.FromBase64String(input);

            try
            {
                AesManaged aes = new AesManaged
                {
                    Key = keyArray,
                    Mode = CipherMode.ECB,
                    Padding = PaddingMode.PKCS7
                };

                ICryptoTransform cTransform = aes.CreateDecryptor();
                byte[] resultArray = cTransform.TransformFinalBlock(toDecryptArray, 0, toDecryptArray.Length);

                return Encoding.UTF8.GetString(resultArray);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Failed to decrypt: " + ex.Message);
                return null;
            }
        }
    }
}