using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace UnlockFps.Utils
{
    public static class RegistryHelper
    {
        private const string KeyPathCN = @"Software\miHoYo\崩坏：星穹铁道";
        private const string KeyPathGL = @"Software\Cognosphere\Star Rail";
        private const string ValueName = "GraphicsSettings_PCResolution_h431323223";

        /// <summary>
        /// 写入分辨率设置到注册表
        /// </summary>
        public static void WriteResolution(int width, int height, bool isFullScreen ,int gameVersion = 0)
        {
            // 确定要使用的注册表路径
            string registryPath = null;
            if (gameVersion == 1)
            {
                registryPath = KeyPathCN;
            }
            else if (gameVersion == 2)
            {
                registryPath = KeyPathGL;
            }
            else
            {
                return; // 未知版本，直接返回
            }

            try
            {
                string json = $"{{\"width\":{width},\"height\":{height},\"isFullScreen\":{isFullScreen.ToString().ToLower()}}}";
                byte[] utf8Bytes = Encoding.UTF8.GetBytes(json);
                byte[] dataWithNull = new byte[utf8Bytes.Length + 1];
                Array.Copy(utf8Bytes, 0, dataWithNull, 0, utf8Bytes.Length);
                dataWithNull[utf8Bytes.Length] = 0;

                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(registryPath, writable: true))
                {
                    if (key == null)
                    {
                        Console.WriteLine($"Registry path does not exist, attempting to create...");

                        using (RegistryKey newKey = Registry.CurrentUser.CreateSubKey(registryPath))
                        {
                            newKey.SetValue(ValueName, dataWithNull, RegistryValueKind.Binary);
                        }
                    }
                    else
                    {
                        key.SetValue(ValueName, dataWithNull, RegistryValueKind.Binary);
                    }
                }

                Console.WriteLine($"Star Rail:Successfully modified: Resolution: {width}x{height}, Fullscreen: {isFullScreen}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Writed Failed：{ex.Message}");
            }
        
        }



        public static void UpdateFullScreenOnly(bool isFullScreen, int gameVersion = 0, int defaultWidth = 1920, int defaultHeight = 1080)
        {
            try
            {
                string keyPath = gameVersion == 1 ? KeyPathCN : KeyPathGL;
                int width = 0, height = 0;
                bool found = false;

                // 尝试读取现有分辨率
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(keyPath))
                {
                    if (key != null)
                    {
                        byte[] existingData = key.GetValue(ValueName) as byte[];
                        if (existingData != null && existingData.Length > 0)
                        {
                            // 去除末尾的 null 终止符
                            int realLength = existingData.Length;
                            if (existingData[existingData.Length - 1] == 0)
                                realLength--;

                            string json = Encoding.UTF8.GetString(existingData, 0, realLength);
                            // 简单解析（建议用 System.Text.Json）
                            var parts = json.Trim('{', '}').Split(',');
                            foreach (var part in parts)
                            {
                                var kv = part.Split(':');
                                if (kv.Length == 2)
                                {
                                    string keyName = kv[0].Trim('"');
                                    string value = kv[1].Trim();
                                    if (keyName == "width")
                                        width = int.Parse(value);
                                    else if (keyName == "height")
                                        height = int.Parse(value);
                                }
                            }
                            found = (width > 0 && height > 0);
                        }
                    }
                }

                // 如果未找到有效分辨率，使用默认值
                if (!found)
                {
                    Console.WriteLine("Existing resolution not found. Using default values.");
                    width = defaultWidth > 0 ? defaultWidth : 1920;
                    height = defaultHeight > 0 ? defaultHeight : 1080;
                }

                // 调用 WriteResolution 写入（它会自动创建路径）
                WriteResolution(width, height, isFullScreen, gameVersion);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to update fullscreen mode: {ex.Message}");
            }
        }


        //public static void UpdateFullScreenOnly(bool isFullScreen ,int gameVersion = 0)
        //{
        //    if (gameVersion == 1)
        //    {
        //        try
        //        {
        //            // 1. 读取现有注册表值
        //            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(KeyPathCN))
        //            {
        //                if (key == null)
        //                {
        //                    Console.WriteLine("注册表路径不存在，无法读取当前分辨率。");
        //                    return;
        //                }

        //                byte[] existingData = key.GetValue(ValueName) as byte[];
        //                if (existingData == null || existingData.Length == 0)
        //                {
        //                    Console.WriteLine("注册表值不存在或为空。");
        //                    return;
        //                }

        //                // 2. 去除末尾的 null 终止符，转换为 JSON 字符串
        //                //    注意：存储时末尾有一个 0，需要去掉
        //                int realLength = existingData.Length;
        //                if (existingData[existingData.Length - 1] == 0)
        //                    realLength--;

        //                string json = Encoding.UTF8.GetString(existingData, 0, realLength);

        //                // 3. 解析 JSON 获取当前 width 和 height
        //                //    这里使用简单的字符串解析，更健壮的方式是使用 JsonDocument
        //                int width = 0, height = 0;
        //                // 简单解析示例（实际建议使用 System.Text.Json）
        //                var parts = json.Trim('{', '}').Split(',');
        //                foreach (var part in parts)
        //                {
        //                    var kv = part.Split(':');
        //                    if (kv.Length == 2)
        //                    {
        //                        string keyName = kv[0].Trim('"');
        //                        string value = kv[1].Trim();
        //                        if (keyName == "width")
        //                            width = int.Parse(value);
        //                        else if (keyName == "height")
        //                            height = int.Parse(value);
        //                    }
        //                }

        //                // 4. 调用原有的写入方法，保持宽高不变，只更新全屏状态
        //                WriteResolution(width, height, isFullScreen,1);
        //            }
        //        }
        //        catch (Exception ex)
        //        {
        //            Console.WriteLine($"更新全屏状态失败：{ex.Message}");
        //        }
        //    }
        //    if(gameVersion == 2)
        //    {
        //        try
        //        {
        //            // 1. 读取现有注册表值
        //            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(KeyPathGL))
        //            {
        //                if (key == null)
        //                {
        //                    Console.WriteLine("注册表路径不存在，无法读取当前分辨率。");
        //                    return;
        //                }

        //                byte[] existingData = key.GetValue(ValueName) as byte[];
        //                if (existingData == null || existingData.Length == 0)
        //                {
        //                    Console.WriteLine("注册表值不存在或为空。");
        //                    return;
        //                }

        //                // 2. 去除末尾的 null 终止符，转换为 JSON 字符串
        //                //    注意：存储时末尾有一个 0，需要去掉
        //                int realLength = existingData.Length;
        //                if (existingData[existingData.Length - 1] == 0)
        //                    realLength--;

        //                string json = Encoding.UTF8.GetString(existingData, 0, realLength);

        //                // 3. 解析 JSON 获取当前 width 和 height
        //                //    这里使用简单的字符串解析，更健壮的方式是使用 JsonDocument
        //                int width = 0, height = 0;
        //                // 简单解析示例（实际建议使用 System.Text.Json）
        //                var parts = json.Trim('{', '}').Split(',');
        //                foreach (var part in parts)
        //                {
        //                    var kv = part.Split(':');
        //                    if (kv.Length == 2)
        //                    {
        //                        string keyName = kv[0].Trim('"');
        //                        string value = kv[1].Trim();
        //                        if (keyName == "width")
        //                            width = int.Parse(value);
        //                        else if (keyName == "height")
        //                            height = int.Parse(value);
        //                    }
        //                }

        //                // 4. 调用原有的写入方法，保持宽高不变，只更新全屏状态
        //                WriteResolution(width, height, isFullScreen,2);
        //            }
        //        }
        //        catch (Exception ex)
        //        {
        //            Console.WriteLine($"更新全屏状态失败：{ex.Message}");
        //        }
        //    }
        //}


        public static (int width, int height, bool isFullScreen)? GetCurrentResolution(int gametype = 0)
        {
            string keyPath = gametype switch
            {
                1 => KeyPathCN,
                2 => KeyPathGL,
                _ => null
            };

            if (keyPath == null) return null;

            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(keyPath))
                {
                    if (key == null)
                    {
                        Console.WriteLine($"Registry path does not exist: HKEY_CURRENT_USER\\{keyPath}");
                        return null;
                    }

                    object value = key.GetValue(ValueName);
                    if (value == null)
                    {
                        Console.WriteLine($"Registry value does not exist: {ValueName}");
                        return null;
                    }

                    if (value is byte[] rawData)
                    {
                        int endIndex = Array.IndexOf(rawData, (byte)0);
                        if (endIndex == -1) endIndex = rawData.Length;
                        byte[] validData = new byte[endIndex];
                        Array.Copy(rawData, 0, validData, 0, endIndex);
                        string jsonString = Encoding.UTF8.GetString(validData);

                        int width = ExtractInt(jsonString, "width");
                        int height = ExtractInt(jsonString, "height");
                        bool isFullScreen = ExtractBool(jsonString, "isFullScreen");

                        return (width, height, isFullScreen);
                    }
                    else
                    {
                        Console.WriteLine("Registry value type is not the expected binary data.");
                        return null;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading the registry: {ex.Message}");
                return null;
            }
        }


        // 私有辅助方法：从 JSON 中提取整数
        private static int ExtractInt(string json, string fieldName)
        {
            string pattern = $"\"{fieldName}\"\\s*:\\s*(\\d+)";
            var match = Regex.Match(json, pattern);
            if (match.Success)
                return int.Parse(match.Groups[1].Value);
            return 0;
        }

        // 私有辅助方法：从 JSON 中提取布尔值
        private static bool ExtractBool(string json, string fieldName)
        {
            string pattern = $"\"{fieldName}\"\\s*:\\s*(true|false)";
            var match = Regex.Match(json, pattern);
            if (match.Success)
                return bool.Parse(match.Groups[1].Value);
            return false;
        }
    }
}

