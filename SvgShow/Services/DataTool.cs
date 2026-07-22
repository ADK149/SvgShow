using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace SvgShow.Services
{
    internal class DataTool
    {
        public static List<GeometryElement> ParseTxtGeometry(string content)
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
                    double startAngle = ParseDouble(match.Groups[4].Value) * Math.PI / 180.0;
                    double endAngle = ParseDouble(match.Groups[5].Value) * Math.PI / 180.0;

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

            // 递归遍历整个JSON，提取 GeometryType=10(线段) 和 GeometryType=14(弧) 的基础单元
            ParseJsonNode(root, elements);

            return elements;
        }

        private static void ParseJsonNode(JsonElement element, List<GeometryElement> elements)
        {
            if (element.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in element.EnumerateArray())
                {
                    ParseJsonNode(item, elements);
                }
            }
            else if (element.ValueKind == JsonValueKind.Object)
            {
                // 如果当前对象是线段基础单元(GeometryType=10)
                if (IsLineUnit(element, out var line))
                {
                    elements.Add(line);
                }
                // 如果当前对象是弧基础单元(GeometryType=14)
                else if (IsArcUnit(element, out var arc))
                {
                    elements.Add(arc);
                }

                // 继续递归遍历所有属性
                foreach (var property in element.EnumerateObject())
                {
                    ParseJsonNode(property.Value, elements);
                }
            }
        }

        private static bool IsLineUnit(JsonElement obj, out GeometryElement line)
        {
            line = null;
            if (obj.ValueKind != JsonValueKind.Object) return false;
            if (!obj.TryGetProperty("GeometryType", out var gt) || gt.ValueKind != JsonValueKind.Number) return false;
            if (gt.GetInt32() != 10) return false;
            if (!obj.TryGetProperty("StartPoint", out var sp) || !obj.TryGetProperty("EndPoint", out var ep)) return false;
            if (!sp.TryGetProperty("X", out var x1) || !sp.TryGetProperty("Y", out var y1)) return false;
            if (!ep.TryGetProperty("X", out var x2) || !ep.TryGetProperty("Y", out var y2)) return false;

            line = new GeometryElement
            {
                Type = "line",
                X1 = x1.GetDouble(),
                Y1 = y1.GetDouble(),
                X2 = x2.GetDouble(),
                Y2 = y2.GetDouble(),
                Stroke = "#4CAF50",
                StrokeWidth = 1
            };
            return true;
        }

        private static bool IsArcUnit(JsonElement obj, out GeometryElement arc)
        {
            arc = null;
            if (obj.ValueKind != JsonValueKind.Object) return false;
            if (!obj.TryGetProperty("GeometryType", out var gt) || gt.ValueKind != JsonValueKind.Number) return false;
            if (gt.GetInt32() != 14) return false;
            if (!obj.TryGetProperty("Circle", out var circle)) return false;
            if (!circle.TryGetProperty("Center", out var center)) return false;
            if (!center.TryGetProperty("X", out var cx) || !center.TryGetProperty("Y", out var cy)) return false;
            if (!circle.TryGetProperty("Radius", out var r)) return false;
            if (!obj.TryGetProperty("StartAngle", out var startAngle)) return false;
            if (!obj.TryGetProperty("DeltaAngle", out var deltaAngle)) return false;
            if (!obj.TryGetProperty("IsClockwise", out var isClockwise)) return false;

            double centerX = cx.GetDouble();
            double centerY = cy.GetDouble();
            double radius = r.GetDouble();
            double start = startAngle.GetDouble();
            double delta = deltaAngle.GetDouble();

            // 顺时针需要反转 delta 符号以转换为 SVG 的逆时针路径
            bool cw = isClockwise.ValueKind == JsonValueKind.True ||
                      (isClockwise.ValueKind == JsonValueKind.String && isClockwise.GetString() == "true");
            double sweepDelta = cw ? -delta : delta;
            double endAngle = start + sweepDelta;

            double x1 = centerX + radius * Math.Cos(start);
            double y1 = centerY + radius * Math.Sin(start);
            double x2 = centerX + radius * Math.Cos(endAngle);
            double y2 = centerY + radius * Math.Sin(endAngle);

            // 角度差绝对值大于180度时需要 largeArcFlag=1
            int largeArcFlag = Math.Abs(sweepDelta) > Math.PI ? 1 : 0;
            // SVG的sweep-flag: 0=逆时针, 1=顺时针
            int sweepFlag = cw ? 1 : 0;

            string d = $"M {x1:F6} {y1:F6} A {radius:F6} {radius:F6} 0 {largeArcFlag} {sweepFlag} {x2:F6} {y2:F6}";

            arc = new GeometryElement
            {
                Type = "path",
                D = d,
                Fill = "none",
                Stroke = "#FF9800",
                StrokeWidth = 1
            };
            return true;
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
