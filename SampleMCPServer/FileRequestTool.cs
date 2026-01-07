using ModelContextProtocol.Server;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace SampleMCPServer
{
    [McpServerToolType]
    public static class FileRequestTool
    {
        // Tool: Get local file content
        [McpServerTool, Description("Get local file's content")]
        public static async Task<string> GetFileContent(
            [Description("Path to the file")] string filePath)
        {
            if (!File.Exists(filePath))
            {
                return $"Error: File not found at {filePath}";
            }

            try
            {
                string fileContent = await File.ReadAllTextAsync(filePath);
                return fileContent;
            }
            catch (Exception ex)
            {
                return $"Error processing file: {ex.Message}";
            }
        }

        // Tool: Read PDF file content
        [McpServerTool, Description("Read and extract text content from a PDF file")]
        public static async Task<string> ReadPdfFile(
            [Description("Path to the PDF file")] string pdfFilePath)
        {
            if (!File.Exists(pdfFilePath))
            {
                return $"Error: PDF file not found at {pdfFilePath}";
            }

            if (!pdfFilePath.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            {
                return $"Error: File must be a PDF (.pdf extension required)";
            }

            try
            {
                StringBuilder extractedText = new StringBuilder();

                await Task.Run(() =>
                {
                    using (PdfDocument document = PdfDocument.Open(pdfFilePath))
                    {
                        extractedText.AppendLine($"PDF Document: {pdfFilePath}");
                        extractedText.AppendLine($"Total Pages: {document.NumberOfPages}");
                        extractedText.AppendLine($"PDF Version: {document.Version}");
                        extractedText.AppendLine();
                        extractedText.AppendLine("=== Content ===");
                        extractedText.AppendLine();

                        for (int pageNumber = 1; pageNumber <= document.NumberOfPages; pageNumber++)
                        {
                            Page page = document.GetPage(pageNumber);
                            string pageText = page.Text;

                            extractedText.AppendLine($"--- Page {pageNumber} ---");
                            extractedText.AppendLine(pageText);
                            extractedText.AppendLine();
                        }
                    }
                });

                return extractedText.ToString();
            }
            catch (Exception ex)
            {
                return $"Error reading PDF file: {ex.Message}";
            }
        }

        // Tool: Get PDF metadata
        [McpServerTool, Description("Get metadata information from a PDF file (author, title, page count, etc.)")]
        public static async Task<string> GetPdfMetadata(
            [Description("Path to the PDF file")] string pdfFilePath)
        {
            if (!File.Exists(pdfFilePath))
            {
                return $"Error: PDF file not found at {pdfFilePath}";
            }

            if (!pdfFilePath.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            {
                return $"Error: File must be a PDF (.pdf extension required)";
            }

            try
            {
                StringBuilder metadata = new StringBuilder();

                await Task.Run(() =>
                {
                    using (PdfDocument document = PdfDocument.Open(pdfFilePath))
                    {
                        metadata.AppendLine("=== PDF Metadata ===");
                        metadata.AppendLine($"File Path: {pdfFilePath}");
                        metadata.AppendLine($"Number of Pages: {document.NumberOfPages}");
                        metadata.AppendLine($"PDF Version: {document.Version}");

                        if (document.Information != null)
                        {
                            var info = document.Information;
                            metadata.AppendLine($"Title: {info.Title ?? "N/A"}");
                            metadata.AppendLine($"Author: {info.Author ?? "N/A"}");
                            metadata.AppendLine($"Subject: {info.Subject ?? "N/A"}");
                            metadata.AppendLine($"Keywords: {info.Keywords ?? "N/A"}");
                            metadata.AppendLine($"Creator: {info.Creator ?? "N/A"}");
                            metadata.AppendLine($"Producer: {info.Producer ?? "N/A"}");
                            metadata.AppendLine($"Creation Date: {info.CreationDate ?? "N/A"}");
                            metadata.AppendLine($"Modified Date: {info.ModifiedDate ?? "N/A"}");
                        }
                    }
                });

                return metadata.ToString();
            }
            catch (Exception ex)
            {
                return $"Error reading PDF metadata: {ex.Message}";
            }
        }

        // Tool: Read specific page from PDF
        [McpServerTool, Description("Read text content from a specific page of a PDF file")]
        public static async Task<string> ReadPdfPage(
            [Description("Path to the PDF file")] string pdfFilePath,
            [Description("Page number to read (1-based index)")] int pageNumber)
        {
            if (!File.Exists(pdfFilePath))
            {
                return $"Error: PDF file not found at {pdfFilePath}";
            }

            if (!pdfFilePath.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            {
                return $"Error: File must be a PDF (.pdf extension required)";
            }

            try
            {
                string pageText = await Task.Run(() =>
                {
                    using (PdfDocument document = PdfDocument.Open(pdfFilePath))
                    {
                        if (pageNumber < 1 || pageNumber > document.NumberOfPages)
                        {
                            return $"Error: Invalid page number. PDF has {document.NumberOfPages} pages.";
                        }

                        Page page = document.GetPage(pageNumber);
                        return $"=== Page {pageNumber} of {document.NumberOfPages} ===\n\n{page.Text}";
                    }
                });

                return pageText;
            }
            catch (Exception ex)
            {
                return $"Error reading PDF page: {ex.Message}";
            }
        }
    }
}
