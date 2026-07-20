using System.IO;
using System.Windows;
using System.Windows.Media;

namespace SvgShow.Services
{
    public class SvgService
    {
        public (DrawingImage Image, Rect Bounds) LoadSvg(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException("SVG文件不存在", filePath);

            var settings = new SharpVectors.Renderers.Wpf.WpfDrawingSettings
            {
                IncludeRuntime = true,
                TextAsGeometry = false
            };

            var converter = new SharpVectors.Converters.FileSvgConverter(settings);
            
            converter.Convert(filePath);
            
            var drawingGroup = converter.Drawing;
            
            if (drawingGroup == null)
                throw new InvalidDataException("无法解析SVG文件");

            var bounds = drawingGroup.Bounds;
            var image = new DrawingImage(drawingGroup);
            
            return (image, bounds);
        }

        public List<(DrawingImage Image, Rect Bounds, string FileName)> LoadSvgs(string[] filePaths)
        {
            var results = new List<(DrawingImage, Rect, string)>();
            foreach (var filePath in filePaths)
            {
                try
                {
                    var (image, bounds) = LoadSvg(filePath);
                    results.Add((image, bounds, Path.GetFileName(filePath)));
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException($"加载SVG文件失败: {filePath}", ex);
                }
            }
            return results;
        }
    }
}