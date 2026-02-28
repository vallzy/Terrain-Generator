using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Pfim;

namespace MapTerrainGeneratorWPF
{
    public class TextureGroup
    {
        public string Name { get; set; }
        public List<TextureSet> Sets { get; set; } = new List<TextureSet>();
    }

    public class TextureSet
    {
        public string Name { get; set; }
        public List<TextureItem> Textures { get; set; } = new List<TextureItem>();
    }

    public class TextureItem
    {
        public string Name { get; set; }
        public string ShortName { get; set; }
        public string FilePath { get; set; }
        public string ArchiveEntry { get; set; }
        public bool IsShader { get; set; }
        public ImageSource Thumbnail { get; set; }
        public string RawShaderText { get; set; } 
    }

    public static class TextureManager
    {
        public static List<object> LoadTextureSets(string gameDataPath)
        {
            var setsDict = new Dictionary<string, TextureSet>(StringComparer.OrdinalIgnoreCase);
            var texturesDict = new Dictionary<string, TextureItem>(StringComparer.OrdinalIgnoreCase);

            if (string.IsNullOrWhiteSpace(gameDataPath) || !Directory.Exists(gameDataPath)) return new List<object>();

            HashSet<string> validShaders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string scriptsDir = Path.Combine(gameDataPath, "scripts");
            string physicalShaderList = Path.Combine(scriptsDir, "shaderlist.txt");

            if (File.Exists(physicalShaderList))
            {
                foreach (var line in File.ReadAllLines(physicalShaderList))
                {
                    string trim = line.Trim();
                    if (!string.IsNullOrEmpty(trim) && !trim.StartsWith("//")) validShaders.Add(trim);
                }
            }

            string[] pk3Files = Directory.GetFiles(gameDataPath, "*.pk3").OrderBy(f => f).ToArray();

            if (validShaders.Count == 0)
            {
                foreach (var pk3 in pk3Files)
                {
                    using (var archive = ZipFile.OpenRead(pk3))
                    {
                        var entry = archive.Entries.FirstOrDefault(e => e.FullName.Equals("scripts/shaderlist.txt", StringComparison.OrdinalIgnoreCase));
                        if (entry != null)
                        {
                            using (var stream = entry.Open())
                            using (var reader = new StreamReader(stream))
                            {
                                string[] lines = reader.ReadToEnd().Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                                foreach (var line in lines)
                                {
                                    string trim = line.Trim();
                                    if (!string.IsNullOrEmpty(trim) && !trim.StartsWith("//")) validShaders.Add(trim);
                                }
                            }
                            break;
                        }
                    }
                }
            }

            foreach (var pk3 in pk3Files)
            {
                using (var archive = ZipFile.OpenRead(pk3))
                {
                    foreach (var entry in archive.Entries)
                    {
                        string lowerName = entry.FullName.ToLowerInvariant();

                        if (lowerName.StartsWith("textures/") && (lowerName.EndsWith(".tga") || lowerName.EndsWith(".jpg") || lowerName.EndsWith(".png")))
                        {
                            string[] parts = lowerName.Split('/');
                            if (parts.Length < 3) continue;
                            string setName = parts[1];
                            string shortName = Path.GetFileNameWithoutExtension(parts.Last());
                            string fullName = $"textures/{setName}/{shortName}";

                            if (!setsDict.ContainsKey(setName)) setsDict[setName] = new TextureSet { Name = setName };
                            texturesDict[fullName] = new TextureItem { Name = fullName, ShortName = shortName, FilePath = pk3, ArchiveEntry = entry.FullName, IsShader = false };
                        }
                        else if (lowerName.StartsWith("scripts/") && lowerName.EndsWith(".shader"))
                        {
                            string shaderFileName = Path.GetFileNameWithoutExtension(lowerName);
                            if (validShaders.Contains(shaderFileName))
                            {
                                using (var stream = entry.Open())
                                using (var reader = new StreamReader(stream))
                                {
                                    ParseShaderContent(reader.ReadToEnd(), pk3, setsDict, texturesDict);
                                }
                            }
                        }
                    }
                }
            }

            string texturesDir = Path.Combine(gameDataPath, "textures");
            if (Directory.Exists(texturesDir))
            {
                foreach (string dir in Directory.GetDirectories(texturesDir))
                {
                    string setName = new DirectoryInfo(dir).Name;
                    if (!setsDict.ContainsKey(setName)) setsDict[setName] = new TextureSet { Name = setName };

                    foreach (string file in Directory.GetFiles(dir, "*.*").Where(f => f.EndsWith(".tga", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".png", StringComparison.OrdinalIgnoreCase)))
                    {
                        string shortName = Path.GetFileNameWithoutExtension(file);
                        string fullName = $"textures/{setName}/{shortName}".ToLowerInvariant();
                        texturesDict[fullName] = new TextureItem { Name = fullName, ShortName = shortName, FilePath = file, ArchiveEntry = null, IsShader = false };
                    }
                }
            }

            if (Directory.Exists(scriptsDir))
            {
                foreach (string shaderFile in Directory.GetFiles(scriptsDir, "*.shader"))
                {
                    string shaderFileName = Path.GetFileNameWithoutExtension(shaderFile);
                    if (validShaders.Contains(shaderFileName))
                    {
                        ParseShaderContent(File.ReadAllText(shaderFile), null, setsDict, texturesDict);
                    }
                }
            }

            foreach (var set in setsDict.Values) set.Textures.Clear();
            foreach (var tex in texturesDict.Values)
            {
                string[] parts = tex.Name.Split('/');
                string setName = parts.Length > 1 ? parts[1] : "unsorted";
                if (setsDict.ContainsKey(setName)) setsDict[setName].Textures.Add(tex);
            }

            var allSets = setsDict.Values.Where(s => s.Textures.Count > 0).OrderBy(s => s.Name).ToList();
            foreach (var set in allSets) set.Textures = set.Textures.OrderBy(t => t.ShortName).ToList();

            var groupedResult = new List<object>();
            var prefixGroups = new Dictionary<string, List<TextureSet>>(StringComparer.OrdinalIgnoreCase);

            foreach (var set in allSets)
            {
                string prefix = set.Name.Split('_')[0];
                if (!prefixGroups.ContainsKey(prefix)) prefixGroups[prefix] = new List<TextureSet>();
                prefixGroups[prefix].Add(set);
            }

            foreach (var kvp in prefixGroups.OrderBy(k => k.Key))
            {
                if (kvp.Value.Count == 1) groupedResult.Add(kvp.Value[0]);
                else groupedResult.Add(new TextureGroup { Name = kvp.Key, Sets = kvp.Value });
            }

            return groupedResult;
        }

        private static void ParseShaderContent(string content, string sourcePk3, Dictionary<string, TextureSet> setsDict, Dictionary<string, TextureItem> texturesDict)
        {
            string[] lines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            string currentShaderName = null;
            string editorImage = null;
            string mapImage = null;
            int depth = 0;

            System.Text.StringBuilder rawShaderBody = new System.Text.StringBuilder();

            foreach (var rawLine in lines)
            {
                string line = rawLine.Trim();
                if (line.StartsWith("//") && depth == 0) continue;
                if (string.IsNullOrEmpty(line)) continue;

                // Build the raw text block
                if (currentShaderName != null || depth == 0) rawShaderBody.AppendLine(rawLine);

                if (line == "{") { depth++; continue; }
                if (line == "}")
                {
                    depth--;
                    if (depth == 0 && currentShaderName != null)
                    {
                        string[] parts = currentShaderName.Split('/');
                        string setName = parts.Length > 1 ? parts[1] : "unsorted";
                        string shortName = parts.Length > 0 ? parts.Last() : currentShaderName;

                        if (!setsDict.ContainsKey(setName)) setsDict[setName] = new TextureSet { Name = setName };

                        string bestImage = editorImage ?? mapImage;
                        if (bestImage != null) bestImage = bestImage.Replace("\\", "/").ToLowerInvariant();

                        string shaderKey = currentShaderName.ToLowerInvariant();
                        if (!texturesDict.ContainsKey(shaderKey))
                        {
                            texturesDict[shaderKey] = new TextureItem { Name = currentShaderName, ShortName = shortName, IsShader = true };
                        }
                        else texturesDict[shaderKey].IsShader = true;

                        texturesDict[shaderKey].RawShaderText = rawShaderBody.ToString();

                        if (bestImage != null)
                        {
                            if (sourcePk3 != null)
                            {
                                texturesDict[shaderKey].FilePath = sourcePk3;
                                texturesDict[shaderKey].ArchiveEntry = bestImage;
                            }
                            else
                            {
                                texturesDict[shaderKey].ArchiveEntry = bestImage;
                            }
                        }

                        currentShaderName = null; editorImage = null; mapImage = null;
                        rawShaderBody.Clear(); 
                    }
                    continue;
                }

                if (depth == 0) currentShaderName = line.Replace("\"", "");
                else if (depth == 1 && line.StartsWith("qer_editorimage", StringComparison.OrdinalIgnoreCase))
                {
                    var tokens = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    if (tokens.Length > 1) editorImage = tokens[1];
                }
                else if (depth == 2 && line.StartsWith("map", StringComparison.OrdinalIgnoreCase) && mapImage == null)
                {
                    var tokens = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    if (tokens.Length > 1 && !tokens[1].StartsWith("$")) mapImage = tokens[1];
                }
            }
        }

        public static ImageSource LoadImage(TextureItem item, string gameDataPath)
        {
            try
            {
                if (item.ArchiveEntry != null && item.FilePath != null && item.FilePath.EndsWith(".pk3", StringComparison.OrdinalIgnoreCase))
                {
                    using (var archive = ZipFile.OpenRead(item.FilePath))
                    {
                        var entry = archive.Entries.FirstOrDefault(e => e.FullName.Equals(item.ArchiveEntry, StringComparison.OrdinalIgnoreCase));
                        if (entry == null) return null;

                        using (var stream = entry.Open())
                        using (var ms = new MemoryStream())
                        {
                            stream.CopyTo(ms);
                            ms.Position = 0;
                            return DecodeImageStream(ms, item.ArchiveEntry);
                        }
                    }
                }
                else
                {
                    string absolutePath = item.FilePath;

                    if (string.IsNullOrWhiteSpace(absolutePath) && item.ArchiveEntry != null)
                        absolutePath = Path.Combine(gameDataPath, item.ArchiveEntry.Replace("/", "\\"));

                    if (string.IsNullOrWhiteSpace(absolutePath) || !File.Exists(absolutePath)) return null;

                    using (var fs = File.OpenRead(absolutePath))
                    {
                        return DecodeImageStream(fs, absolutePath);
                    }
                }
            }
            catch { return null; }
        }

        private static ImageSource DecodeImageStream(Stream stream, string fileName)
        {
            if (fileName.EndsWith(".tga", StringComparison.OrdinalIgnoreCase))
            {
                using (var image = Pfimage.FromStream(stream))
                {
                    PixelFormat format = PixelFormats.Bgra32;
                    byte[] rawData = image.Data;

                    if (image.Format == Pfim.ImageFormat.Rgb24)
                    {
                        format = PixelFormats.Bgr24;
                    }
                    else if (image.Format == Pfim.ImageFormat.Rgba32)
                    {
                        format = PixelFormats.Bgra32;
                        for (int i = 3; i < rawData.Length; i += 4)
                        {
                            rawData[i] = 255;
                        }
                    }

                    var bitmap = BitmapSource.Create(image.Width, image.Height, 96.0, 96.0, format, null, rawData, image.Stride);
                    bitmap.Freeze();
                    return bitmap;
                }
            }
            else
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.StreamSource = stream;
                bitmap.EndInit();
                bitmap.Freeze();
                return bitmap;
            }
        }
    }
}