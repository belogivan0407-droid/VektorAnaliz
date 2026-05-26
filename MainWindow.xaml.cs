using System;
using System.Windows;
using System.Windows.Media;
using System.Numerics;
using System.Text.RegularExpressions;
using HelixToolkit.Wpf;
using NCalc;

using WpfMedia = System.Windows.Media.Media3D;
using HelixGeo = HelixToolkit.Geometry;

namespace Vektoranaliz
{
    public partial class MainWindow : Window
    {
        private WpfMedia.MeshGeometry3D currentMesh = new WpfMedia.MeshGeometry3D();
        private WpfMedia.ModelVisual3D currentVectorMarker = null;
        private WpfMedia.ModelVisual3D currentSurfaceModel = null;
        private Action currentSurfaceGenerator = null;

        private NCalc.Expression exprX, exprY, exprZ;
        private bool isFieldValid = false;

        public MainWindow()
        {
            InitializeComponent();
            Loaded += (s, e) => AddCustomSurface_Click(null, null);
        }

        private Vector3 V3(double x, double y, double z) => new Vector3((float)x, (float)y, (float)z);

        private WpfMedia.MeshGeometry3D ConvertMesh(HelixGeo.MeshGeometry3D hMesh)
        {
            var wMesh = new WpfMedia.MeshGeometry3D();
            if (hMesh.Positions != null)
                foreach (var p in hMesh.Positions) wMesh.Positions.Add(new WpfMedia.Point3D(p.X, p.Y, p.Z));
            if (hMesh.TriangleIndices != null)
                foreach (var idx in hMesh.TriangleIndices) wMesh.TriangleIndices.Add(idx);
            return wMesh;
        }

        private void BuildGridIndices(WpfMedia.MeshGeometry3D wMesh, int uRes, int vRes, bool reverseWinding = false, int offset = 0)
        {
            for (int i = 0; i < uRes; i++)
            {
                for (int j = 0; j < vRes; j++)
                {
                    int p1 = offset + i * (vRes + 1) + j;
                    int p2 = p1 + 1;
                    int p3 = offset + (i + 1) * (vRes + 1) + j;
                    int p4 = p3 + 1;

                    if (reverseWinding)
                    {
                        wMesh.TriangleIndices.Add(p1); wMesh.TriangleIndices.Add(p3); wMesh.TriangleIndices.Add(p2);
                        wMesh.TriangleIndices.Add(p2); wMesh.TriangleIndices.Add(p3); wMesh.TriangleIndices.Add(p4);
                    }
                    else
                    {
                        wMesh.TriangleIndices.Add(p1); wMesh.TriangleIndices.Add(p2); wMesh.TriangleIndices.Add(p3);
                        wMesh.TriangleIndices.Add(p2); wMesh.TriangleIndices.Add(p4); wMesh.TriangleIndices.Add(p3);
                    }
                }
            }
        }

        private void ShowHelp_Click(object sender, RoutedEventArgs e)
        {
            string helpText =
                "РУКОВОДСТВО ПОЛЬЗОВАТЕЛЯ: ВЕКТОРНЫЙ АНАЛИЗ\n\n" +
        "1. ЗАДАНИЕ ВЕКТОРНОГО ПОЛЯ A(x,y,z):\n" +
        "Введите выражения для компонент поля Ax, Ay, Az. Допускается использование переменных x, y, z и функций (sin, cos, exp, sqrt, abs, pi).\n" +
        "Пример: 2x, xy, sin(x^2).\n\n" +
        "2. ПОСТРОЕНИЕ ПОВЕРХНОСТИ:\n" +
        "- 'Свои поверхности': введите уравнение z=f(x,y). Можно строить сразу две поверхности (z1 - синяя, z2 - зеленая) для демонстрации их пересечения.\n" +
        "- 'Шаблоны': выберите готовое тело (сфера, конус и т.д.). Укажите параметры a, b, c для задания размеров.\n\n" +
        "3. ОГРАНИЧЕНИЯ (ОТРАСЕЛЬ):\n" +
        "В поле 'Ограничение' введите неравенство, которое отсекает область (например: x^2 + y^2 <= 4).\n" +
        "Программа вырежет из поверхности только тот кусок, где условие выполняется (True).\n\n" +
        "4. РАСЧЕТЫ И АНАЛИЗ:\n" +
        "- 'Пересчитать поток': запускает алгоритм численного интегрирования. Вычисляется поток Ф через всю видимую поверхность.\n" +
        "- Площадь (S): суммарная площадь всех полигонов, попавших в область.\n" +
        "- Анализ точки: Наведите курсор на 3D-модель. Панель 'Данные в точке курсора' покажет вектор нормали n, вектор поля A и плотность потока (A·n) в конкретной точке.\n\n" +
        "5. ИНТЕРПРЕТАЦИЯ ЦВЕТА:\n" +
        "Поверхность окрашивается градиентом:\n" +
        "- КРАСНЫЙ: поток вытекает (A·n > 0).\n" +
        "- СИНИЙ: поток втекает (A·n < 0).\n" +
        "- БЕЛЫЙ: поток равен 0 (вектор поля касается поверхности).\n\n" +
        "Ориентация нормали в меню определяет, какая сторона поверхности считается 'внешней'.";

            MessageBox.Show(helpText, "Инструкция к программе", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private string PrepareMathExpression(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return "0";

            input = input.ToLower()
                .Replace(" или ", " or ")
                .Replace(" и ", " and ")
                .Replace("sin", "Sin").Replace("cos", "Cos")
                .Replace("tan", "Tan").Replace("sqrt", "Sqrt")
                .Replace("abs", "Abs").Replace("exp", "Exp")
                .Replace("pi", "Pi").Replace("pow", "Pow");

            var regexPow = new Regex(@"([a-zA-Z0-9_.]+|\([^)]+\))\s*\^\s*([a-zA-Z0-9_.]+|\([^)]+\))");
            while (regexPow.IsMatch(input)) input = regexPow.Replace(input, "Pow($1, $2)");

            input = Regex.Replace(input, @"(\d)([a-zA-Z])", "$1*$2");
            input = input.Replace("xy", "x*y").Replace("yx", "y*x")
                         .Replace("xz", "x*z").Replace("zx", "z*x")
                         .Replace("yz", "y*z").Replace("zy", "z*y");

            return input;
        }

        private double GetParam(System.Windows.Controls.TextBox tb, double def)
        {
            if (double.TryParse(tb.Text.Replace(".", ","), out double val)) return val;
            if (double.TryParse(tb.Text.Replace(",", "."), out val)) return val;
            return def;
        }

        private bool UpdateVectorFieldExpressions()
        {
            try
            {
                exprX = new NCalc.Expression(PrepareMathExpression(TextBoxAx.Text));
                exprY = new NCalc.Expression(PrepareMathExpression(TextBoxAy.Text));
                exprZ = new NCalc.Expression(PrepareMathExpression(TextBoxAz.Text));
                isFieldValid = true;
                EvaluateVectorField(0, 0, 0);
                ErrorText.Text = "";
                return true;
            }
            catch
            {
                ErrorText.Text = "Ошибка в формуле поля! Проверьте синтаксис.";
                FluxResult.Text = "Поток Ф = ---";
                AreaResult.Text = "Площадь S = ---";
                isFieldValid = false;
                return false;
            }
        }

        private WpfMedia.Vector3D EvaluateVectorField(double x, double y, double z)
        {
            if (!isFieldValid) return new WpfMedia.Vector3D(0, 0, 0);
            try
            {
                exprX.Parameters["x"] = x; exprX.Parameters["y"] = y; exprX.Parameters["z"] = z;
                exprY.Parameters["x"] = x; exprY.Parameters["y"] = y; exprY.Parameters["z"] = z;
                exprZ.Parameters["x"] = x; exprZ.Parameters["y"] = y; exprZ.Parameters["z"] = z;

                return new WpfMedia.Vector3D(
                    Convert.ToDouble(exprX.Evaluate()),
                    Convert.ToDouble(exprY.Evaluate()),
                    Convert.ToDouble(exprZ.Evaluate())
                );
            }
            catch { return new WpfMedia.Vector3D(0, 0, 0); }
        }

        // --- МАТЕМАТИКА 1: Границы по области определения функции (NaN) ---
        private WpfMedia.Point3D FindExactBoundary(double badX, double badY, double goodX, double goodY, NCalc.Expression expr)
        {
            double lastZ = 0;
            for (int k = 0; k < 10; k++)
            {
                double midX = (goodX + badX) / 2.0;
                double midY = (goodY + badY) / 2.0;
                expr.Parameters["x"] = midX; expr.Parameters["y"] = midY;
                double z = double.NaN;
                try { z = Convert.ToDouble(expr.Evaluate()); } catch { }

                if (!double.IsNaN(z) && !double.IsInfinity(z)) { goodX = midX; goodY = midY; lastZ = z; }
                else { badX = midX; badY = midY; }
            }
            return new WpfMedia.Point3D(goodX, goodY, lastZ);
        }

        private WpfMedia.MeshGeometry3D GenerateCustomMesh(string formula)
        {
            if (string.IsNullOrWhiteSpace(formula)) return null;
            var wMesh = new WpfMedia.MeshGeometry3D();

            int uRes = 150, vRes = 150;
            double minX = -4.0, maxX = 4.0, minY = -4.0, maxY = 4.0;

            NCalc.Expression surfExpr;
            try
            {
                surfExpr = new NCalc.Expression(PrepareMathExpression(formula));
                surfExpr.Parameters["x"] = 0.0; surfExpr.Parameters["y"] = 0.0; surfExpr.Evaluate();
            }
            catch { return null; }

            WpfMedia.Point3D[,] points = new WpfMedia.Point3D[uRes + 1, vRes + 1];
            bool[,] isValid = new bool[uRes + 1, vRes + 1];
            bool[,] isEdge = new bool[uRes + 1, vRes + 1];

            for (int i = 0; i <= uRes; i++)
            {
                double x = minX + (maxX - minX) * i / uRes;
                for (int j = 0; j <= vRes; j++)
                {
                    double y = minY + (maxY - minY) * j / vRes;
                    surfExpr.Parameters["x"] = x; surfExpr.Parameters["y"] = y;

                    double z = 0; bool valid = true;
                    try { z = Convert.ToDouble(surfExpr.Evaluate()); if (double.IsNaN(z) || double.IsInfinity(z)) valid = false; }
                    catch { valid = false; }

                    isValid[i, j] = valid;
                    points[i, j] = new WpfMedia.Point3D(x, y, valid ? z : double.NaN);
                }
            }

            int[] di = { -1, 1, 0, 0, -1, -1, 1, 1 }; int[] dj = { 0, 0, -1, 1, -1, 1, -1, 1 };
            for (int i = 0; i <= uRes; i++)
            {
                for (int j = 0; j <= vRes; j++)
                {
                    if (!isValid[i, j])
                    {
                        int vI = -1, vJ = -1;
                        for (int d = 0; d < 8; d++)
                        {
                            int ni = i + di[d], nj = j + dj[d];
                            if (ni >= 0 && ni <= uRes && nj >= 0 && nj <= vRes && isValid[ni, nj]) { vI = ni; vJ = nj; break; }
                        }
                        if (vI != -1)
                        {
                            points[i, j] = FindExactBoundary(points[i, j].X, points[i, j].Y, points[vI, vJ].X, points[vI, vJ].Y, surfExpr);
                            isEdge[i, j] = true;
                        }
                    }
                }
            }

            for (int i = 0; i <= uRes; i++)
            {
                for (int j = 0; j <= vRes; j++)
                {
                    if (isValid[i, j] || isEdge[i, j]) wMesh.Positions.Add(points[i, j]);
                    else wMesh.Positions.Add(new WpfMedia.Point3D(double.NaN, double.NaN, double.NaN));
                }
            }

            for (int i = 0; i < uRes; i++)
            {
                for (int j = 0; j < vRes; j++)
                {
                    int p1 = i * (vRes + 1) + j, p2 = p1 + 1, p3 = (i + 1) * (vRes + 1) + j, p4 = p3 + 1;
                    if (p4 >= wMesh.Positions.Count) continue;

                    bool v1 = isValid[i, j] || isEdge[i, j], v2 = isValid[i, j + 1] || isEdge[i, j + 1];
                    bool v3 = isValid[i + 1, j] || isEdge[i + 1, j], v4 = isValid[i + 1, j + 1] || isEdge[i + 1, j + 1];

                    if (v1 && v2 && v3) { wMesh.TriangleIndices.Add(p1); wMesh.TriangleIndices.Add(p2); wMesh.TriangleIndices.Add(p3); }
                    if (v2 && v4 && v3) { wMesh.TriangleIndices.Add(p2); wMesh.TriangleIndices.Add(p4); wMesh.TriangleIndices.Add(p3); }
                }
            }
            return wMesh;
        }

        // --- МАТЕМАТИКА 2: Идеальный срез по условиям отсечения (Marching Triangles) ---
        private bool CheckCond(double x, double y, double z, NCalc.Expression condExpr)
        {
            if (condExpr == null) return true;
            if (double.IsNaN(x) || double.IsNaN(y) || double.IsNaN(z)) return false;
            condExpr.Parameters["x"] = x; condExpr.Parameters["y"] = y; condExpr.Parameters["z"] = z;
            try { object res = condExpr.Evaluate(); return res is bool b && b; } catch { return false; }
        }

        private WpfMedia.Point3D FindEdge3D(WpfMedia.Point3D pGood, WpfMedia.Point3D pBad, NCalc.Expression condExpr)
        {
            WpfMedia.Point3D good = pGood;
            WpfMedia.Point3D bad = pBad;
            for (int i = 0; i < 12; i++) // 12 шагов = микроскопическая точность среза
            {
                WpfMedia.Point3D mid = new WpfMedia.Point3D((good.X + bad.X) / 2.0, (good.Y + bad.Y) / 2.0, (good.Z + bad.Z) / 2.0);
                if (CheckCond(mid.X, mid.Y, mid.Z, condExpr)) good = mid;
                else bad = mid;
            }
            return good;
        }

        private WpfMedia.MeshGeometry3D ApplyCondition(WpfMedia.MeshGeometry3D baseMesh)
        {
            string condition = TbCondition.Text.Trim();
            if (string.IsNullOrEmpty(condition) || baseMesh == null) return baseMesh;

            var resultMesh = new WpfMedia.MeshGeometry3D();
            NCalc.Expression condExpr;
            try { condExpr = new NCalc.Expression(PrepareMathExpression(condition)); }
            catch { ErrorText.Text = "Ошибка в условии ограничения!"; return baseMesh; }

            int idx = 0;
            for (int i = 0; i < baseMesh.TriangleIndices.Count; i += 3)
            {
                if (i + 2 >= baseMesh.TriangleIndices.Count) break;

                int i1 = baseMesh.TriangleIndices[i], i2 = baseMesh.TriangleIndices[i + 1], i3 = baseMesh.TriangleIndices[i + 2];
                if (i1 < 0 || i1 >= baseMesh.Positions.Count || i2 < 0 || i2 >= baseMesh.Positions.Count || i3 < 0 || i3 >= baseMesh.Positions.Count) continue;

                var p1 = baseMesh.Positions[i1]; var p2 = baseMesh.Positions[i2]; var p3 = baseMesh.Positions[i3];
                if (double.IsNaN(p1.X) || double.IsNaN(p2.X) || double.IsNaN(p3.X)) continue;

                bool v1 = CheckCond(p1.X, p1.Y, p1.Z, condExpr);
                bool v2 = CheckCond(p2.X, p2.Y, p2.Z, condExpr);
                bool v3 = CheckCond(p3.X, p3.Y, p3.Z, condExpr);

                int count = (v1 ? 1 : 0) + (v2 ? 1 : 0) + (v3 ? 1 : 0);

                if (count == 3) // Треугольник полностью внутри ограничения
                {
                    resultMesh.Positions.Add(p1); resultMesh.Positions.Add(p2); resultMesh.Positions.Add(p3);
                    resultMesh.TriangleIndices.Add(idx++); resultMesh.TriangleIndices.Add(idx++); resultMesh.TriangleIndices.Add(idx++);
                }
                else if (count == 1) // 1 хорошая точка, 2 плохие -> Образует 1 маленький треугольник
                {
                    WpfMedia.Point3D pGood = v1 ? p1 : (v2 ? p2 : p3);
                    WpfMedia.Point3D pNext = v1 ? p2 : (v2 ? p3 : p1);
                    WpfMedia.Point3D pPrev = v1 ? p3 : (v2 ? p1 : p2);

                    WpfMedia.Point3D e1 = FindEdge3D(pGood, pNext, condExpr);
                    WpfMedia.Point3D e2 = FindEdge3D(pGood, pPrev, condExpr);

                    resultMesh.Positions.Add(pGood); resultMesh.Positions.Add(e1); resultMesh.Positions.Add(e2);
                    resultMesh.TriangleIndices.Add(idx++); resultMesh.TriangleIndices.Add(idx++); resultMesh.TriangleIndices.Add(idx++);
                }
                else if (count == 2) // 2 хорошие точки, 1 плохая -> Образует четырехугольник (2 треугольника)
                {
                    WpfMedia.Point3D pBad = !v1 ? p1 : (!v2 ? p2 : p3);
                    WpfMedia.Point3D pNext = !v1 ? p2 : (!v2 ? p3 : p1);
                    WpfMedia.Point3D pPrev = !v1 ? p3 : (!v2 ? p1 : p2);

                    WpfMedia.Point3D e1 = FindEdge3D(pNext, pBad, condExpr);
                    WpfMedia.Point3D e2 = FindEdge3D(pPrev, pBad, condExpr);

                    resultMesh.Positions.Add(pNext); resultMesh.Positions.Add(pPrev); resultMesh.Positions.Add(e2);
                    resultMesh.TriangleIndices.Add(idx++); resultMesh.TriangleIndices.Add(idx++); resultMesh.TriangleIndices.Add(idx++);

                    resultMesh.Positions.Add(pNext); resultMesh.Positions.Add(e2); resultMesh.Positions.Add(e1);
                    resultMesh.TriangleIndices.Add(idx++); resultMesh.TriangleIndices.Add(idx++); resultMesh.TriangleIndices.Add(idx++);
                }
            }
            return resultMesh;
        }

        private WpfMedia.MeshGeometry3D GenerateConditionWallMesh()
        {
            string condition = TbCondition.Text.Trim();
            if (string.IsNullOrEmpty(condition) || condition.Contains("z")) return null;

            var wallMesh = new WpfMedia.MeshGeometry3D();
            NCalc.Expression condExpr;
            try { condExpr = new NCalc.Expression(PrepareMathExpression(condition)); } catch { return null; }

            double minX = -4.0, maxX = 4.0, minY = -4.0, maxY = 4.0, minZ = -3.0, maxZ = 4.0, step = 0.04;
            int stepsX = (int)((maxX - minX) / step), stepsY = (int)((maxY - minY) / step);
            bool[,] grid = new bool[stepsX + 1, stepsY + 1];

            for (int i = 0; i <= stepsX; i++)
            {
                double x = minX + i * step;
                for (int j = 0; j <= stepsY; j++)
                {
                    double y = minY + j * step;
                    condExpr.Parameters["x"] = x; condExpr.Parameters["y"] = y; condExpr.Parameters["z"] = 0.0;
                    try { object res = condExpr.Evaluate(); grid[i, j] = (res is bool b) ? b : false; } catch { grid[i, j] = false; }
                }
            }

            int idx = 0;
            for (int i = 0; i < stepsX; i++)
            {
                double x1 = minX + i * step, x2 = x1 + step;
                for (int j = 0; j < stepsY; j++)
                {
                    double y1 = minY + j * step, y2 = y1 + step;
                    if (grid[i, j] != grid[i + 1, j]) AddVerticalQuad(wallMesh, x1, y1, x1, y2, minZ, maxZ, ref idx);
                    if (grid[i, j] != grid[i, j + 1]) AddVerticalQuad(wallMesh, x1, y1, x2, y1, minZ, maxZ, ref idx);
                }
            }
            return wallMesh;
        }

        private void AddVerticalQuad(WpfMedia.MeshGeometry3D mesh, double x1, double y1, double x2, double y2, double zMin, double zMax, ref int idx)
        {
            mesh.Positions.Add(new WpfMedia.Point3D(x1, y1, zMin)); mesh.Positions.Add(new WpfMedia.Point3D(x2, y2, zMin));
            mesh.Positions.Add(new WpfMedia.Point3D(x1, y1, zMax)); mesh.Positions.Add(new WpfMedia.Point3D(x2, y2, zMax));

            mesh.TriangleIndices.Add(idx); mesh.TriangleIndices.Add(idx + 1); mesh.TriangleIndices.Add(idx + 2);
            mesh.TriangleIndices.Add(idx + 1); mesh.TriangleIndices.Add(idx + 3); mesh.TriangleIndices.Add(idx + 2);
            mesh.TriangleIndices.Add(idx); mesh.TriangleIndices.Add(idx + 2); mesh.TriangleIndices.Add(idx + 1);
            mesh.TriangleIndices.Add(idx + 1); mesh.TriangleIndices.Add(idx + 3); mesh.TriangleIndices.Add(idx + 2);
            idx += 4;
        }

        private void ProcessAndRenderSurface(WpfMedia.MeshGeometry3D rawMesh)
        {
            var cutMesh = ApplyCondition(rawMesh);
            currentMesh = cutMesh;

            if (cutMesh.Positions.Count == 0 || !UpdateVectorFieldExpressions())
            {
                ModelContainer.Children.Clear();
                return;
            }

            int sign = CbNormalDir != null && CbNormalDir.SelectedIndex == 0 ? 1 : -1;
            double totalFlux = 0;
            double totalArea = 0;

            var vertexNormals = new WpfMedia.Vector3D[cutMesh.Positions.Count];
            int[] vertexFaceCounts = new int[cutMesh.Positions.Count];

            for (int i = 0; i < cutMesh.TriangleIndices.Count; i += 3)
            {
                int i1 = cutMesh.TriangleIndices[i], i2 = cutMesh.TriangleIndices[i + 1], i3 = cutMesh.TriangleIndices[i + 2];
                var p1 = cutMesh.Positions[i1]; var p2 = cutMesh.Positions[i2]; var p3 = cutMesh.Positions[i3];

                var v1 = new WpfMedia.Vector3D(p2.X - p1.X, p2.Y - p1.Y, p2.Z - p1.Z);
                var v2 = new WpfMedia.Vector3D(p3.X - p1.X, p3.Y - p1.Y, p3.Z - p1.Z);
                var cross = WpfMedia.Vector3D.CrossProduct(v1, v2);

                double area = cross.Length * 0.5;
                totalArea += area;

                // --- ЗАЩИТА ОТ ПОЛЮСОВ ---
                var n = cross;
                if (n.Length < 1e-6) n = new WpfMedia.Vector3D(0, 0, 1); // Безопасная нормаль
                else n.Normalize();
                n *= sign;

                vertexNormals[i1] += n; vertexFaceCounts[i1]++;
                vertexNormals[i2] += n; vertexFaceCounts[i2]++;
                vertexNormals[i3] += n; vertexFaceCounts[i3]++;

                double cx = (p1.X + p2.X + p3.X) / 3.0, cy = (p1.Y + p2.Y + p3.Y) / 3.0, cz = (p1.Z + p2.Z + p3.Z) / 3.0;
                var fieldA = EvaluateVectorField(cx, cy, cz);

                // Добавляем только если результат расчета поля не NaN
                double partialFlux = (fieldA.X * n.X + fieldA.Y * n.Y + fieldA.Z * n.Z) * area;
                if (!double.IsNaN(partialFlux)) totalFlux += partialFlux;
            }

            FluxResult.Text = $"Поток Ф = {totalFlux:F4}";
            AreaResult.Text = $"Площадь S = {totalArea:F4}";

            double[] densities = new double[cutMesh.Positions.Count];
            double maxAbsDensity = 1e-6;

            cutMesh.Normals.Clear();
            for (int i = 0; i < cutMesh.Positions.Count; i++)
            {
                var n = vertexNormals[i];
                if (vertexFaceCounts[i] > 0) n /= vertexFaceCounts[i];

                if (n.Length < 1e-6) n = new WpfMedia.Vector3D(0, 0, 1);
                n.Normalize();
                cutMesh.Normals.Add(n);

                var p = cutMesh.Positions[i];
                var fieldA = EvaluateVectorField(p.X, p.Y, p.Z);
                double d = fieldA.X * n.X + fieldA.Y * n.Y + fieldA.Z * n.Z;
                densities[i] = double.IsNaN(d) ? 0 : d;

                if (Math.Abs(densities[i]) > maxAbsDensity) maxAbsDensity = Math.Abs(densities[i]);
            }

            cutMesh.TextureCoordinates.Clear();
            for (int i = 0; i < cutMesh.Positions.Count; i++)
            {
                double u = 0.5 - (densities[i] / (2 * maxAbsDensity));
                cutMesh.TextureCoordinates.Add(new Point(u, 0.5));
            }

            var heatMapBrush = new LinearGradientBrush { StartPoint = new Point(0, 0.5), EndPoint = new Point(1, 0.5) };
            heatMapBrush.GradientStops.Add(new GradientStop(Color.FromRgb(255, 123, 84), 0.0));
            heatMapBrush.GradientStops.Add(new GradientStop(Color.FromRgb(238, 243, 222), 0.5));
            heatMapBrush.GradientStops.Add(new GradientStop(Color.FromRgb(95, 184, 255), 1.0));

            var material = new WpfMedia.DiffuseMaterial(heatMapBrush);
            var model = new WpfMedia.GeometryModel3D(cutMesh, material) { BackMaterial = material };
            currentSurfaceModel = new WpfMedia.ModelVisual3D { Content = model };
            ModelContainer.Children.Add(currentSurfaceModel);
        }

        private void SetupNewSurface(WpfMedia.MeshGeometry3D baseMesh, WpfMedia.MeshGeometry3D secondCustomMesh = null)
        {
            ModelContainer.Children.Clear();
            currentVectorMarker = null;

            ProcessAndRenderSurface(baseMesh);

            if (secondCustomMesh != null && secondCustomMesh.Positions.Count > 0)
            {
                var cutSecondMesh = ApplyCondition(secondCustomMesh);
                if (cutSecondMesh.Positions.Count > 0)
                {
                    var secondMaterial = MaterialHelper.CreateMaterial(Colors.LimeGreen, 0.35);
                    var secondModel = new WpfMedia.GeometryModel3D(cutSecondMesh, secondMaterial) { BackMaterial = secondMaterial };
                    ModelContainer.Children.Add(new WpfMedia.ModelVisual3D { Content = secondModel });
                }
            }

            if (CbDrawAsSecond != null && CbDrawAsSecond.IsChecked == true)
            {
                var wallMesh = GenerateConditionWallMesh();
                if (wallMesh != null && wallMesh.Positions.Count > 0)
                {
                    var ghostMaterial = MaterialHelper.CreateMaterial(Colors.DarkOrange, 0.25);
                    var ghostModel = new WpfMedia.GeometryModel3D(wallMesh, ghostMaterial) { BackMaterial = ghostMaterial };
                    ModelContainer.Children.Add(new WpfMedia.ModelVisual3D { Content = ghostModel });
                }
            }

            Viewport.ZoomExtents();
        }

        private void Viewport_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (currentMesh.Positions.Count == 0 || !isFieldValid) return;
            Point mousePos = e.GetPosition(Viewport);
            bool isHit = Viewport3DHelper.FindNearest(Viewport.Viewport, mousePos, out WpfMedia.Point3D hitPoint, out WpfMedia.Vector3D normal, out DependencyObject visual);

            if (isHit && visual != currentVectorMarker)
            {
                DrawFieldVector(hitPoint);

                int sign = CbNormalDir != null && CbNormalDir.SelectedIndex == 0 ? 1 : -1;
                normal *= sign;

                var f = EvaluateVectorField(hitPoint.X, hitPoint.Y, hitPoint.Z);
                double density = f.X * normal.X + f.Y * normal.Y + f.Z * normal.Z;

                HoverPoint.Text = $"Точка: ({hitPoint.X:F2}, {hitPoint.Y:F2}, {hitPoint.Z:F2})";
                HoverNormal.Text = $"Нормаль n: ({normal.X:F2}, {normal.Y:F2}, {normal.Z:F2})";
                HoverField.Text = $"Поле A: ({f.X:F2}, {f.Y:F2}, {f.Z:F2})";
                HoverDensity.Text = $"Плотность (A·n): {density:F4}";
            }
        }

        private void DrawFieldVector(WpfMedia.Point3D p)
        {
            if (currentVectorMarker != null) ModelContainer.Children.Remove(currentVectorMarker);
            var f = EvaluateVectorField(p.X, p.Y, p.Z);
            var mb = new HelixGeo.MeshBuilder();
            mb.AddArrow(V3(p.X, p.Y, p.Z), V3(p.X + f.X * 0.3, p.Y + f.Y * 0.3, p.Z + f.Z * 0.3), 0.05f);
            currentVectorMarker = new WpfMedia.ModelVisual3D { Content = new WpfMedia.GeometryModel3D(ConvertMesh(mb.ToMesh()), Materials.Yellow) };
            ModelContainer.Children.Add(currentVectorMarker);
        }

        private void AddCustomSurface_Click(object sender, RoutedEventArgs e)
        {
            currentSurfaceGenerator = () => {
                var wMesh1 = GenerateCustomMesh(TextBoxCustomSurface1.Text);
                WpfMedia.MeshGeometry3D wMesh2 = null;
                if (CbEnableSecondSurface.IsChecked == true) wMesh2 = GenerateCustomMesh(TextBoxCustomSurface2.Text);
                if (wMesh1 != null) SetupNewSurface(wMesh1, wMesh2);
            };
            currentSurfaceGenerator.Invoke();
        }

        private void Recalculate_Click(object sender, RoutedEventArgs e) { if (currentSurfaceGenerator != null) currentSurfaceGenerator.Invoke(); }
        private void CheckBox_Changed(object sender, RoutedEventArgs e) { if (currentSurfaceGenerator != null && IsLoaded) currentSurfaceGenerator.Invoke(); }

        private void AddEllipsoid_Click(object sender, RoutedEventArgs e)
        {
            currentSurfaceGenerator = () => {
                var wMesh = new WpfMedia.MeshGeometry3D(); int uRes = 80, vRes = 80;
                double a = GetParam(TbA, 2.0), b = GetParam(TbB, 2.0), c = GetParam(TbC, 2.0);
                for (int i = 0; i <= uRes; i++)
                {
                    double phi = Math.PI * i / uRes;
                    for (int j = 0; j <= vRes; j++) wMesh.Positions.Add(new WpfMedia.Point3D(a * Math.Sin(phi) * Math.Cos(2 * Math.PI * j / vRes), b * Math.Sin(phi) * Math.Sin(2 * Math.PI * j / vRes), c * Math.Cos(phi)));
                }
                BuildGridIndices(wMesh, uRes, vRes, true); SetupNewSurface(wMesh);
            }; currentSurfaceGenerator.Invoke();
        }

        private void AddOneSheetHyperboloid_Click(object sender, RoutedEventArgs e)
        {
            currentSurfaceGenerator = () => {
                var wMesh = new WpfMedia.MeshGeometry3D(); int uRes = 80, vRes = 80; double a = GetParam(TbA, 1.0), b = GetParam(TbB, 1.0), c = GetParam(TbC, 1.2);
                for (int i = 0; i <= uRes; i++)
                {
                    double u = -1.2 + 2.4 * i / uRes;
                    for (int j = 0; j <= vRes; j++) wMesh.Positions.Add(new WpfMedia.Point3D(a * Math.Cosh(u) * Math.Cos(2 * Math.PI * j / vRes), b * Math.Cosh(u) * Math.Sin(2 * Math.PI * j / vRes), c * Math.Sinh(u)));
                }
                BuildGridIndices(wMesh, uRes, vRes, false); SetupNewSurface(wMesh);
            }; currentSurfaceGenerator.Invoke();
        }

        private void AddTwoSheetHyperboloid_Click(object sender, RoutedEventArgs e)
        {
            currentSurfaceGenerator = () => {
                var wMesh = new WpfMedia.MeshGeometry3D(); int uRes = 40, vRes = 80; double a = GetParam(TbA, 1.0), b = GetParam(TbB, 1.0), c = GetParam(TbC, 1.0);
                for (int i = 0; i <= uRes; i++) { double u = 0.0 + 1.3 * i / uRes; for (int j = 0; j <= vRes; j++) wMesh.Positions.Add(new WpfMedia.Point3D(a * Math.Sinh(u) * Math.Cos(2 * Math.PI * j / vRes), b * Math.Sinh(u) * Math.Sin(2 * Math.PI * j / vRes), c * Math.Cosh(u))); }
                BuildGridIndices(wMesh, uRes, vRes, false); int offset = wMesh.Positions.Count;
                for (int i = 0; i <= uRes; i++) { double u = 0.0 + 1.3 * i / uRes; for (int j = 0; j <= vRes; j++) wMesh.Positions.Add(new WpfMedia.Point3D(a * Math.Sinh(u) * Math.Cos(2 * Math.PI * j / vRes), b * Math.Sinh(u) * Math.Sin(2 * Math.PI * j / vRes), -c * Math.Cosh(u))); }
                BuildGridIndices(wMesh, uRes, vRes, true, offset); SetupNewSurface(wMesh);
            }; currentSurfaceGenerator.Invoke();
        }

        private void AddEllipticCone_Click(object sender, RoutedEventArgs e)
        {
            currentSurfaceGenerator = () => {
                var wMesh = new WpfMedia.MeshGeometry3D(); int uRes = 80, vRes = 80; double a = GetParam(TbA, 1.0), b = GetParam(TbB, 1.0), c = GetParam(TbC, 1.0);
                for (int i = 0; i <= uRes; i++) { double u = -1.5 + 3.0 * i / uRes; for (int j = 0; j <= vRes; j++) wMesh.Positions.Add(new WpfMedia.Point3D(a * u * Math.Cos(2 * Math.PI * j / vRes), b * u * Math.Sin(2 * Math.PI * j / vRes), c * u)); }
                BuildGridIndices(wMesh, uRes, vRes, false); SetupNewSurface(wMesh);
            }; currentSurfaceGenerator.Invoke();
        }

        private void AddParaboloid_Click(object sender, RoutedEventArgs e)
        {
            currentSurfaceGenerator = () => {
                var wMesh = new WpfMedia.MeshGeometry3D(); int res = 80; double a = GetParam(TbA, 1.0), b = GetParam(TbB, 1.0), c = GetParam(TbC, 2.0);
                for (int i = 0; i <= res; i++) { double r = c * i / res; for (int j = 0; j <= res; j++) { double x = r * Math.Cos(2 * Math.PI * j / res); double y = r * Math.Sin(2 * Math.PI * j / res); wMesh.Positions.Add(new WpfMedia.Point3D(x, y, (x * x) / (a * a) + (y * y) / (b * b))); } }
                BuildGridIndices(wMesh, res, res, false); SetupNewSurface(wMesh);
            }; currentSurfaceGenerator.Invoke();
        }

        private void AddHyperbolicParaboloid_Click(object sender, RoutedEventArgs e)
        {
            currentSurfaceGenerator = () => {
                var wMesh = new WpfMedia.MeshGeometry3D(); int uRes = 80, vRes = 80; double a = GetParam(TbA, 1.0), b = GetParam(TbB, 1.0);
                for (int i = 0; i <= uRes; i++) { double x = -2.0 + 4.0 * i / uRes; for (int j = 0; j <= vRes; j++) { double y = -2.0 + 4.0 * j / vRes; wMesh.Positions.Add(new WpfMedia.Point3D(x, y, (x * x) / (a * a) - (y * y) / (b * b))); } }
                BuildGridIndices(wMesh, uRes, vRes, false); SetupNewSurface(wMesh);
            }; currentSurfaceGenerator.Invoke();
        }

        private void AddEllipticCylinder_Click(object sender, RoutedEventArgs e)
        {
            currentSurfaceGenerator = () => {
                var wMesh = new WpfMedia.MeshGeometry3D(); int uRes = 80, vRes = 80; double a = GetParam(TbA, 1.5), b = GetParam(TbB, 1.0);
                for (int i = 0; i <= uRes; i++) { double z = -3.0 + 6.0 * i / uRes; for (int j = 0; j <= vRes; j++) wMesh.Positions.Add(new WpfMedia.Point3D(a * Math.Cos(2 * Math.PI * j / vRes), b * Math.Sin(2 * Math.PI * j / vRes), z)); }
                BuildGridIndices(wMesh, uRes, vRes, false); SetupNewSurface(wMesh);
            }; currentSurfaceGenerator.Invoke();
        }
    }
}