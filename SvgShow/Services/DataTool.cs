using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace SvgShow.Services
{
    internal class DataTool
    {
        public static List<GeometryElement> ParseGeometry(string content)
        {
            var elements = new List<GeometryElement>();
            if (string.IsNullOrWhiteSpace(content)) return elements;
            string[] datas = content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);  
            
            foreach (var data in datas)
            {
                // 解析 SEGMENT: SEGMENT((x1,y1) (x2,y2)) 或 SEGMENT((x1 y1) (x2 y2))
                var segmentMatches = Regex.Matches(data, DataPattern.SEGMENT);
                foreach (Match match in segmentMatches)
                {
                    double x1 = ParseDouble(match.Groups[1].Value);
                    double y1 = ParseDouble(match.Groups[2].Value);
                    double x2 = ParseDouble(match.Groups[3].Value);
                    double y2 = ParseDouble(match.Groups[4].Value);

                    elements.Add(new GeometryElement
                    {
                        Type = "line",
                        X1 = x1,
                        Y1 = y1,
                        X2 = x2,
                        Y2 = y2,
                        Stroke = "#2196F3",
                        StrokeWidth = 1
                    });
                }

                // 解析 ARC: ARC(CIRCLE((cx cy)r)startAngle endAngle)
                var arcMatches = Regex.Matches(data, DataPattern.ARC);
                foreach (Match match in arcMatches)
                {
                    double cx = ParseDouble(match.Groups[1].Value);
                    double cy = ParseDouble(match.Groups[2].Value);
                    double r = ParseDouble(match.Groups[3].Value);
                    double startAngle = ParseDouble(match.Groups[4].Value);
                    double endAngle = ParseDouble(match.Groups[5].Value);

                    double x1 = cx + r * Math.Cos(startAngle);
                    double y1 = cy + r * Math.Sin(startAngle);
                    double x2 = cx + r * Math.Cos(endAngle);
                    double y2 = cy + r * Math.Sin(endAngle);

                    double angleDiff = NormalizeAngle(endAngle - startAngle);
                    int largeArcFlag = angleDiff > Math.PI ? 1 : 0;

                    string d = $"M {x1:F6} {y1:F6} A {r:F6} {r:F6} 0 {largeArcFlag} 1 {x2:F6} {y2:F6}";

                    elements.Add(new GeometryElement
                    {
                        Type = "path",
                        D = d,
                        Fill = "none",
                        Stroke = "#FF9800",
                        StrokeWidth = 1
                    });
                }

                // 解析 Point3d: (x, y, z) - 绘制为圆点（忽略Z坐标，按2D投影）
                var pointMatches = Regex.Matches(data, DataPattern.Point3d);
                foreach (Match match in pointMatches)
                {
                    double x = ParseDouble(match.Groups[1].Value);
                    double y = ParseDouble(match.Groups[2].Value);

                    elements.Add(new GeometryElement
                    {
                        Type = "circle",
                        Cx = x,
                        Cy = y,
                        R = 3,
                        Fill = "red",
                        Stroke = "none"
                    });
                }
            }
            return elements;
        }

        public static List<GeometryElement> ParseJsonGeometry(string jsonContent)
        {
            var elements = new List<GeometryElement>();
            if (string.IsNullOrWhiteSpace(jsonContent)) return elements;

            using var doc = JsonDocument.Parse(jsonContent);
            var root = doc.RootElement;

            if (root.ValueKind != JsonValueKind.Array) return elements;

            foreach (var item in root.EnumerateArray())
            {
                if (!item.TryGetProperty("StartPoint", out var startPoint) ||
                    !item.TryGetProperty("EndPoint", out var endPoint))
                {
                    continue;
                }

                double x1 = GetJsonDouble(startPoint, "X");
                double y1 = GetJsonDouble(startPoint, "Y");
                double x2 = GetJsonDouble(endPoint, "X");
                double y2 = GetJsonDouble(endPoint, "Y");

                elements.Add(new GeometryElement
                {
                    Type = "line",
                    X1 = x1,
                    Y1 = y1,
                    X2 = x2,
                    Y2 = y2,
                    Stroke = "#4CAF50",
                    StrokeWidth = 1
                });
            }

            return elements;
        }

        private static double GetJsonDouble(JsonElement element, string propertyName)
        {
            if (element.TryGetProperty(propertyName, out var prop))
            {
                if (prop.ValueKind == JsonValueKind.Number)
                    return prop.GetDouble();
                if (prop.ValueKind == JsonValueKind.String && double.TryParse(prop.GetString(), out var val))
                    return val;
            }
            return 0;
        }

        private static double ParseDouble(string value)
        {
            return double.Parse(value, CultureInfo.InvariantCulture);
        }

        private static double NormalizeAngle(double angle)
        {
            while (angle < 0) angle += 2 * Math.PI;
            while (angle > 2 * Math.PI) angle -= 2 * Math.PI;
            return angle;
        }
    }

    public class GeometryElement
    {
        public string Type { get; set; } = "";
        public double? Cx { get; set; }
        public double? Cy { get; set; }
        public double? R { get; set; }
        public double? X1 { get; set; }
        public double? Y1 { get; set; }
        public double? X2 { get; set; }
        public double? Y2 { get; set; }
        public string? D { get; set; }
        public string Fill { get; set; } = "none";
        public string Stroke { get; set; } = "black";
        public double? StrokeWidth { get; set; } = 1;
    }

    public class DataPattern
    {
        public readonly static string DoubleData = @"(-?\d+(?:\.\d+)?)";
        public readonly static string Space = @"\s*";

        public readonly static string OriginPoint3dPatten = @"( double , double , double )";
        public readonly static string Point3d = @"\(\s*(-?\d+(?:\.\d+)?)\s*,\s*(-?\d+(?:\.\d+)?)\s*,\s*(-?\d+(?:\.\d+)?)\s*\)";

        public readonly static string OriginARCPattern = @"ARC(CIRCLE((double double)double)double double)";
        public readonly static string ARC = @"ARC\s*\(\s*CIRCLE\s*\(\s*\(\s*(-?\d+(?:\.\d+)?)\s*(-?\d+(?:\.\d+)?)\s*\)\s*(-?\d+(?:\.\d+)?)\s*\)\s*(-?\d+(?:\.\d+)?)\s+(-?\d+(?:\.\d+)?)\s*\)";

        public readonly static string OriginSEGMENTPattern = @"SEGMENT((double double)(double double))";
        public readonly static string SEGMENT = @"SEGMENT\s*\(\s*\(\s*(-?\d+(?:\.\d+)?)\s*(-?\d+(?:\.\d+)?)\s*\)\s*\(\s*(-?\d+(?:\.\d+)?)\s*(-?\d+(?:\.\d+)?)\s*\)\s*\)";
    }
}
