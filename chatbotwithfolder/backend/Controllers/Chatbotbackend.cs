using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class Chatbotbackend : ControllerBase
    {
        private readonly HttpClient _httpClient;

        public Chatbotbackend(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        [HttpPost]
        public async Task<IActionResult> Post([FromBody] ChatRequest request)
        {
            var response = await GetGoogleGenerativeAIResponse(request.Message);
            var filesAndContents = ExtractFilesAndContents(response);

            var projectPath = CreateProject();
            SaveFilesToProject(filesAndContents, projectPath);

            var folderStructure = GetFolderStructure(projectPath);

            return Ok(new { Response = response, FolderStructure = folderStructure });
        }

        private async Task<string> GetGoogleGenerativeAIResponse(string message)
        {
            var googleGenerativeAiServiceUrl = "http://localhost:5000/generate"; // The URL of your Node.js service

            try
            {
                var payload = new { prompt = message };
                var jsonPayload = JsonSerializer.Serialize(payload);
                var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(googleGenerativeAiServiceUrl, content);
                response.EnsureSuccessStatusCode();

                var responseString = await response.Content.ReadAsStringAsync();
                return responseString; // Return the raw JSON response
            }
            catch (Exception ex)
            {
                return $"{{ \"error\": \"{ex.Message}\" }}"; // Return error JSON
            }
        }

         private Dictionary<string, string> ExtractFilesAndContents(string response)
        {
            var filesAndContents = new Dictionary<string, string>();

            // Regex pattern to match file sections, capturing the file name and its content
            var filePattern = new Regex(@"```(?<extension>typescript|html|css)\n// (?<filename>[^/]+)\n(?<content>.*?)\n```", RegexOptions.Singleline);
            var missingNamePattern = new Regex(@"```(?<extension>typescript|html|css)\n(?<content>.*?)\n```", RegexOptions.Singleline);

            var matches = filePattern.Matches(response);

            foreach (Match match in matches)
            {
                var filename = match.Groups["filename"].Value.Trim();
                var content = match.Groups["content"].Value.Trim();
                var extension = match.Groups["extension"].Value.Trim();

                if (!string.IsNullOrEmpty(filename) && !string.IsNullOrEmpty(content))
                {
                    filesAndContents[filename] = content;
                }
            }

            var missingNameMatches = missingNamePattern.Matches(response);

            foreach (Match match in missingNameMatches)
            {
                var content = match.Groups["content"].Value.Trim();
                var extension = match.Groups["extension"].Value.Trim();

                if (!string.IsNullOrEmpty(content))
                {
                    var defaultFilename = $"component.{extension}";
                    filesAndContents[defaultFilename] = content;
                }
            }

            return filesAndContents;
        }


        private string CreateProject()
        {
            var projectName = "GeneratedAngularProject";
            var location = @"Folders";
            var projectPath = Path.Combine(location, projectName);

            try
            {
                // Ensure the base location exists
                if (!Directory.Exists(location))
                {
                    Directory.CreateDirectory(location);
                }

                // Command to create the Angular project
                var angularCliCommand = $"/c ng new {projectName} --skip-install";

                // Create the process to run the Angular CLI command
                var processInfo = new ProcessStartInfo("cmd.exe", angularCliCommand)
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = location // Set working directory to ensure correct paths
                };

                // Start the process and wait for it to exit
                using (var process = Process.Start(processInfo))
                {
                    if (process != null)
                    {
                        process.WaitForExit();
                        var output = process.StandardOutput.ReadToEnd();
                        var error = process.StandardError.ReadToEnd();

                        // Log output and error for debugging
                        Console.WriteLine($"Output: {output}");
                        Console.WriteLine($"Error: {error}");

                        if (process.ExitCode != 0)
                        {
                            throw new Exception($"Failed to create project: {error}");
                        }
                    }
                    else
                    {
                        throw new Exception("Failed to start the process.");
                    }
                }

                // Ensure the project path exists after the command
                if (!Directory.Exists(projectPath))
                {
                    throw new Exception($"Project path does not exist: {projectPath}");
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Error in CreateProject: {ex.Message}");
            }

            return projectPath;
        }

        private void SaveFilesToProject(Dictionary<string, string> filesAndContents, string projectPath)
        {
            var srcAppPath = Path.Combine(projectPath, "src", "app");

            foreach (var file in filesAndContents)
            {
                var filePath = Path.Combine(srcAppPath, file.Key);
                var directoryPath = Path.GetDirectoryName(filePath);

                // Ensure the directory exists
                if (!Directory.Exists(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                }

                // Write file content
                System.IO.File.WriteAllText(filePath, file.Value);
            }
        }

        private object GetFolderStructure(string path)
        {
            var directoryInfo = new DirectoryInfo(path);
            return GetDirectoryStructure(directoryInfo);
        }

        private object GetDirectoryStructure(DirectoryInfo directoryInfo)
        {
            return new
            {
                name = directoryInfo.Name,
                files = directoryInfo.GetFiles().Select(file => new { name = file.Name, content = System.IO.File.ReadAllText(file.FullName) }).ToList(),
                folders = directoryInfo.GetDirectories().Select(GetDirectoryStructure).ToList()
            };
        }
    }

    public class ChatRequest
    {
        public string? Message { get; set; }
    }
}
