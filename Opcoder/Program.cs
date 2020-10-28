using System;
using System.Diagnostics;
using Machina;
using Lumina;
using Machina.FFXIV;
using Lumina.Excel.GeneratedSheets;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Runtime.InteropServices;
using System.Drawing.Imaging;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Collections.Specialized;

namespace Opcoder
{
    class Program
    {
        public static Lumina.Lumina lumina = null;
        public static DateTime lastPosPackageTime = DateTime.Now;
        public static DateTime lastItemPackageTime = DateTime.Now;
        public static Dictionary<int, string> PosDict = new Dictionary<int, string>();
        public static byte startingByteOfItemList = 0;
        public static bool upload = true;
        public static void Main(string[] args)
        {
            FFXIVNetworkMonitor monitor = new FFXIVNetworkMonitor();
            var GameDir = Path.Combine(Process.GetProcessesByName("ffxiv_dx11")[0].MainModule.FileName, "..\\sqpack");
            lumina = new Lumina.Lumina(GameDir);
            monitor.MessageReceived = (string connection, long epoch, byte[] message) => MessageReceived(connection, epoch, message);
            monitor.MessageSent = (string connection, long epoch, byte[] message) => MessageSent(connection, epoch, message);
            monitor.Start();
            // Run for 12000 seconds
            System.Threading.Thread.Sleep(12000000);
            monitor.Stop();
        }
        private static void MessageSent(string connection, long epoch, byte[] message)
        {
            return;
            Console.WriteLine($"MessageSent Opcode:{BitConverter.ToUInt16(message, 18)} Length:{message.Length}");
            if (BitConverter.ToUInt16(message, 18) == 0x3A1)
            {
                return;
                //Console.WriteLine(BitConverter.ToString(message[0..32]).Replace("-", " "));
                //Console.WriteLine(BitConverter.ToString(message[32..]).Replace("-", " "));
                byte[] posArr = message[32..];
                var x = BitConverter.ToSingle(posArr, 12);
                var y = BitConverter.ToSingle(posArr, 16);
                var z = BitConverter.ToSingle(posArr, 20);
                var rotate = BitConverter.ToSingle(posArr, 24);
                Console.WriteLine(BitConverter.ToString(posArr[12..28]).Replace("-", " "));
                Console.WriteLine($"Sent: ({x}, {y}, {z}, {rotate})");
                Console.Beep();
            }

        }
        private static void MessageReceived(string connection, long epoch, byte[] message)
        {
            // Process Data
            // Console.WriteLine($"MessageReceived Opcode:{BitConverter.ToUInt16(message, 18)} Length:{message.Length}");
            if (BitConverter.ToUInt16(message, 18) == 0x22B)
            {
                //Console.WriteLine("Loading Housing List");
                //Console.WriteLine(BitConverter.ToString(message[0..32]).Replace("-", " "));
                var data_list = message[32..];
                var data_header = data_list[..8];
                string area = "";
                if (data_header[4] == 0x53)
                {
                    area = "海雾村";
                }
                else if (data_header[4] == 0x54)
                {
                    area = "薰衣草苗圃";
                }
                else if(data_header[4] == 0x55)
                {
                    area = "高脚孤丘";
                }
                else if(data_header[4] == 0x81)
                {
                    area = "白银乡";
                }
                int slot = data_header[2];
                for (int i=8; i < data_list.Length; i += 40)
                {
                    var house_id = (i - 8) / 40;
                    var name_header = data_list[i..(i + 8)];
                    int flag = name_header[4];
                    bool is_open = (flag & 0b10) > 0;
                    bool has_welcome = (flag & 0b100) > 0;
                    bool is_fc = (flag & 0b10000) > 0;
                    int price = BitConverter.ToInt32(name_header[..4]);
                    string size = (price > 30000000) ? "L" : ((price > 10000000) ? "M" : "S");
                    var name_array = data_list[(i + 8)..(i + 40)];
                    if (name_array[0] == 0)
                    {
                        string text = $"{area} 第{slot + 1}区 {house_id + 1}号 {size}房在售 当前价格:{price}";
                        Console.WriteLine(text);
                        try
                        {
                            if (upload)
                            {
                                string urls = File.ReadAllText(@".\post_url.txt");
                                foreach (string url in urls.Split("\n"))
                                {
                                    string post_url = url.Trim();
                                    if (post_url == "") continue;
                                    Console.WriteLine($"上报消息给 {post_url}");
                                    using var wb = new WebClient();
                                    var data = new NameValueCollection
                                    {
                                        { "text", text }
                                    };
                                    var response = wb.UploadValues(post_url, "POST", data);
                                    string responseInString = Encoding.UTF8.GetString(response);
                                    Console.WriteLine("上报成功");
                                }
                            }
                        }
                        catch (FileNotFoundException)
                        {
                            //Console.WriteLine("未能定位 post_url.txt 上报终止");
                            upload = false;
                        }
                        catch(Exception e)
                        {
                            Console.WriteLine("上报失败 " + e.Message);
                        }

                    }
                    /*
                    var name = Encoding.UTF8.GetString(name_array);
                    string appeal_string = "";
                    foreach (var appeal_id in name_header[5..])
                    {
                        if (appeal_id == 0) continue;
                        var appeaal = lumina.GetExcelSheet<HousingAppeal>(Lumina.Data.Language.ChineseSimplified).GetRow((uint)appeal_id);
                        if (appeal_string != "") appeal_string += " ";
                        appeal_string += appeaal.Tag;
                    }
                    if (is_fc)
                    {
                        name = "《" + name + "》";
                    }
                    string open = is_open ? "开放" : "关闭";
                    Console.WriteLine($"{name}\t{size}\t{open}\t{appeal_string}");
                    */
                }
            }
            if (BitConverter.ToUInt16(message, 18) == 0x353)
            {
                return;
                if (BitConverter.ToString(message[32..]).Replace("-", " ").StartsWith("FF FF FF FF FF FF FF FF"))
                {
                    PosDict.Clear();
                    return;
                }
                /*
                Console.WriteLine(BitConverter.ToString(message[0..32]).Replace("-", " "));
                Console.WriteLine(BitConverter.ToString(message[32..]).Replace("-", " "));
                */
                if (DateTime.Now > lastPosPackageTime.AddSeconds(5))
                {
                    PosDict.Clear();
                    lastPosPackageTime = DateTime.Now;
                }
                byte[] posArr = message[32..];
                for (int i = 12; i < posArr.Length && i + 24 < posArr.Length; i += 24)
                {
                    var furnitureId = BitConverter.ToUInt16(posArr, i);
                    var item = lumina.GetExcelSheet<HousingFurniture>(Lumina.Data.Language.ChineseSimplified).GetRow((uint)(furnitureId + 196608)).Item.Value;
                    if (item.RowId == 0) continue;
                    var rotate = BitConverter.ToSingle(posArr, i + 8);
                    var x = BitConverter.ToSingle(posArr, i + 12);
                    var y = BitConverter.ToSingle(posArr, i + 16);
                    var z = BitConverter.ToSingle(posArr, i + 20);
                    var posSig = BitConverter.ToString(posArr[(i + 12)..(i + 24)]).Replace("-", " ") + " ?? ?? ?? ?? " +
                        BitConverter.ToString(posArr[(i + 8)..(i + 12)]).Replace("-", " ");
                    var posStr = $"({x}, {y}, {z}, {rotate})";
                    Console.WriteLine($"#{PosDict.Count} Item#{item.RowId}:{item.Name} ({x}, {y}, {z}, {rotate})");
                    //Console.WriteLine($"sig: {posSig}");
                    if (!PosDict.ContainsKey(PosDict.Count))
                        PosDict.Add(PosDict.Count, posStr);
                }
                Console.WriteLine($"PosDict loaded {PosDict.Count} items.");

            }
            if (message[18] == 0x69)
            {
                return;
                byte[] itemArr = message[32..];
                if (DateTime.Now > lastItemPackageTime.AddSeconds(5))
                {
                    startingByteOfItemList = itemArr[0];
                    lastItemPackageTime = DateTime.Now;
                }
                ushort ItemId = BitConverter.ToUInt16(message, 48);
                //if (ItemId != 20733) return;
                /*
                Console.WriteLine("Length:" + message.Length + " MessageType: " + message[18].ToString("X2") + " " + message[19].ToString("X2"));
                Console.WriteLine(BitConverter.ToString(message[0..32]).Replace("-", " "));
                Console.WriteLine(BitConverter.ToString(message[32..]).Replace("-", " "));
                */
                var item = lumina.GetExcelSheet<Item>(Lumina.Data.Language.ChineseSimplified).GetRow(ItemId);
                if (item.ItemSearchCategory.Value.Category != 4) return;
                if (item.ItemSearchCategory.Value.Order == 2) return;
                int itemIdx = itemArr[10];
                itemIdx += ((int)itemArr[0] - startingByteOfItemList) * 50;
                string posStr = PosDict.ContainsKey(itemIdx) ? PosDict[itemIdx] : "null";
                Console.WriteLine($"{ItemId} {item.Name} {posStr}");

            }
        }
    }
}