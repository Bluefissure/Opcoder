using System;
using System.Diagnostics;
using Machina;
using Lumina;
using Machina.FFXIV;
using Lumina.Excel.GeneratedSheets;
using System.Collections.Generic;
using System.Linq;

namespace Opcoder
{
    class Program
    {
        public static Lumina.Lumina lumina = null;
        public static DateTime lastPosPackageTime = DateTime.Now;
        public static DateTime lastItemPackageTime = DateTime.Now;
        public static Dictionary<int, string> PosDict = new Dictionary<int, string>();
        public static byte startingByteOfItemList = 0;
        public static void Main(string[] args)
        {
            FFXIVNetworkMonitor monitor = new FFXIVNetworkMonitor();
            lumina = new Lumina.Lumina("C:\\Games\\SDO\\FFXIV\\game\\sqpack");
            monitor.MessageReceived = (string connection, long epoch, byte[] message) => MessageReceived(connection, epoch, message);
            monitor.MessageSent = (string connection, long epoch, byte[] message) => MessageSent(connection, epoch, message);
            monitor.Start();
            // Run for 12000 seconds
            System.Threading.Thread.Sleep(12000000);
            monitor.Stop();
        }
        private static void MessageSent(string connection, long epoch, byte[] message)
        {

            if (BitConverter.ToUInt16(message, 18) == 0x3A1)
            {
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
            /*
            if (BitConverter.ToString(message).Replace("-", " ").IndexOf("BB D5 48 40") != -1 )
            {
                Console.WriteLine("Rotation found! Length:" + message.Length + " MessageType: " + message[18].ToString("X2") + " " + message[19].ToString("X2"));
                Console.WriteLine(BitConverter.ToString(message[0..32]).Replace("-", " "));
                Console.WriteLine(BitConverter.ToString(message[32..]).Replace("-", " "));
            }*/
            if (BitConverter.ToUInt16(message, 18) == 0x353)
            {
                if (BitConverter.ToString(message[32..]).Replace("-", " ").StartsWith("FF FF FF FF FF FF FF FF"))
                {
                    PosDict.Clear();
                    return;
                }
                Console.WriteLine(BitConverter.ToString(message[0..32]).Replace("-", " "));
                Console.WriteLine(BitConverter.ToString(message[32..]).Replace("-", " "));
                int idxOff = 0;
                if (DateTime.Now > lastPosPackageTime.AddSeconds(5))
                {
                    PosDict.Clear();
                    lastPosPackageTime = DateTime.Now;
                }
                else
                {
                    idxOff = PosDict.Count;
                }
                byte [] posArr = message[32..];
                for (int i = 20, idx = 0;i<posArr.Length && i + 16 < posArr.Length; i += 24, idx += 1)
                {
                    var rotate = BitConverter.ToSingle(posArr, i);
                    var x = BitConverter.ToSingle(posArr, i + 4);
                    var y = BitConverter.ToSingle(posArr, i + 8);
                    var z = BitConverter.ToSingle(posArr, i + 12);
                    var posSig = BitConverter.ToString(posArr[(i + 4)..(i + 16)]).Replace("-", " ") + " ?? ?? ?? ?? " +
                        BitConverter.ToString(posArr[(i)..(i + 4)]).Replace("-", " ");
                    var posStr = $"({x}, {y}, {z}, {rotate})";
                    Console.WriteLine($"#{idxOff + idx} ({x}, {y}, {z}, {rotate})\tsig: {posSig}");
                    PosDict.Add(idxOff + idx, posStr);
                }
                Console.WriteLine($"PosDict loaded {PosDict.Count} items.");

            }
            if (message[18] == 0x69)
            {
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
