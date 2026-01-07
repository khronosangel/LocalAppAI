using ModelContextProtocol.Server;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;

namespace SampleMCPServer
{
    [McpServerToolType]
    public static class FileTempWriterTool
    {
        // Tool: Get local file content
        [McpServerTool, Description("Write a local temp file with text content")]
        public static async Task<string> WriteTextFileContent(
            [Description("Filename of TempFile")] string TempFileName,
            [Description("Contents of TempFile")] string TempFileContent)
        {
            string basePath = "C:\\Temp\\MCP";// Path.GetTempPath();
            try
            {
                string filePath = $"{basePath}\\{TempFileName}";
                await File.WriteAllTextAsync(filePath, TempFileContent);
                FileInfo fileInfo = new FileInfo(filePath);
                long fileSzie = fileInfo.Length;
                return $"Success : Filesize = {fileSzie}";
            }
            catch (Exception ex)
            {
                return $"Error processing file: {ex.Message}";
            }
        }

        [McpServerTool, Description("Write a local temp file with binary content")]
        public static async Task<string> WriteBinaryFileContent(
            [Description("Filename of TempFile")] string TempFileName,
            [Description("Contents of TempFile in base64")] string TempFileContentBase64)
        {
            string basePath = "C:\\Temp\\MCP";// Path.GetTempPath();
            try
            {
                string filePath = $"{basePath}\\{TempFileName}";
                byte[] TempFileContent = Convert.FromBase64String(TempFileContentBase64);
                await File.WriteAllBytesAsync(filePath, TempFileContent);
                FileInfo fileInfo = new FileInfo(filePath);
                long fileSzie = fileInfo.Length;
                return $"Success : Filesize = {fileSzie}";
            }
            catch (Exception ex)
            {
                return $"Error processing file: {ex.Message}";
            }
        }
    }
}
