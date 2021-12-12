using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace TcpClient.Modules
{
    public class ConnectionSettings
    {
        public string IpAddress { get; set; }
        public int Port { get; set; }
        public string Message { get; set; }
        public string EndString { get; set; }
    }
    public class ConnectionSettingsManager
    {
        private const string DIR = "SaveFolder";
        private const string FILE_NAME = "last_connection_settings";
        public void SaveConnectionSettings(ConnectionSettings settings)
        {
            using var streamWriter = new StreamWriter(GetConnectionSettingsFilePath(), false, Encoding.GetEncoding("Shift_JIS"));
            streamWriter.WriteLine(JsonConvert.SerializeObject(settings));
        }

        public ConnectionSettings? GetConnectionSettings()
        {
            try
            {
                using var streamReader = new StreamReader(GetConnectionSettingsFilePath(), Encoding.GetEncoding("Shift_JIS"));
                string serializedObject = streamReader.ReadToEnd();
                return JsonConvert.DeserializeObject<ConnectionSettings>(serializedObject);
            } catch (Exception ex)
            {
                return null;
            }
            
        }
        public string GetConnectionSettingsFilePath()
        {
            string saveDirectory = "";
            try
            {
                saveDirectory = Path.Combine(Windows.Storage.ApplicationData.Current.LocalFolder.Path, DIR);
            }
            catch
            {
                saveDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), DIR);
            }
            // ディレクトリの作成
            FileUtility.SafeCreateDirectory(saveDirectory);

            var filePath = Path.Combine(saveDirectory, FILE_NAME);
            FileUtility.SafeCreateFile(filePath);
            // 保存ファイル名、保存パスの取得
            return filePath;
        }
    }

    public static class FileUtility
    {
        public static DirectoryInfo SafeCreateDirectory(string path)
        {
            if (Directory.Exists(path))
            {
                return null;
            }
            return Directory.CreateDirectory(path);
        }

        public static void SafeCreateFile(string path)
        {
            if (File.Exists(path))
            {
                return;
            }
            //ファイルの作成
            using (var file = File.Create(path))
            {
            }
        }
    }
}
