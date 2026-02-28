using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace MapTerrainGeneratorWPF
{
    public static class TerrainEngine
    {
        public static bool GenerateAndExport(
            string mode, string filePath, double manWidth, double manLength, double manHeight,
            double stepX, double stepY, string topTexture, int shapeType, double shapeHeight,
            double terraceStep, int noiseType, double variance, double frequency,
            string outputName, bool overrideMap, Action<string> log, double tunnelHeight = 0)
        {
            try
            {
                string[] originalLines = new string[0];

                if (mode.ToLower() == "hint")
                {
                    if (!File.Exists(filePath))
                    {
                        log("Error: Target .map file not found.");
                        return false;
                    }
                    originalLines = File.ReadAllLines(filePath);
                }

                var target = GetTargetBrushData(mode, originalLines, manWidth, manLength, manHeight, log);
                if (target == null) return false;

                AdjustBoundsToFitGrid(target, stepX, stepY, log);
                log($"Final Terrain Grid: {target.WidthX}x{target.LengthY} | Step Size: X:{stepX} Y:{stepY}");

                string newFuncGroup;
                if (shapeType == 7 || shapeType == 8) // Tunnel / Slope Tunnel
                {
                    double caveHeight = shapeType == 7 ? shapeHeight : (tunnelHeight > 0 ? tunnelHeight : shapeHeight);
                    double slopeHeight = shapeType == 8 ? shapeHeight : 0;
                    var (floorMap, ceilingMap, leftWallMap, rightWallMap, stepZ) = GenerateTunnelHeightMaps(target, stepX, stepY, caveHeight, slopeHeight, variance, frequency, noiseType, terraceStep);
                    newFuncGroup = GenerateTunnelFuncGroups(target, stepX, stepY, stepZ, topTexture, floorMap, ceilingMap, leftWallMap, rightWallMap, caveHeight, slopeHeight);
                }
                else
                {
                    bool splitDiagonally = variance > 0 || shapeType > 0;
                    var heightMap = GenerateHeightMap(target, stepX, stepY, shapeType, shapeHeight, variance, frequency, noiseType, terraceStep);
                    newFuncGroup = GenerateFuncGroup(target, stepX, stepY, splitDiagonally, topTexture, heightMap);
                }

                return ExportFile(mode, originalLines, target, newFuncGroup, filePath, outputName, overrideMap, "", log);
            }
            catch (Exception ex)
            {
                log($"Critical Error: {ex.Message}");
                return false;
            }
        }

        public static BrushData GetTargetBrushData(string mode, string[] originalLines, double w, double l, double h, Action<string> log)
        {
            if (mode.ToLower() == "hint")
            {
                var target = FindValidHintBrush(originalLines);
                if (target == null) log("Error: No valid func_group with exactly 1 common/hint brush found.");
                return target;
            }
            else // manual
            {
                if (w <= 0 || l <= 0 || h <= 0) { log("Error: Dimensions must be > 0."); return null; }
                if (w > 131072 || l > 131072 || h > 131072) { log("Error: Dimensions exceed Radiant limits (131072)."); return null; }

                return new BrushData
                {
                    MinX = -w / 2,
                    MaxX = w / 2,
                    MinY = -l / 2,
                    MaxY = l / 2,
                    MinZ = 0,
                    MaxZ = h,
                    WidthX = w,
                    LengthY = l,
                    HeightZ = h,
                    StartLineIndex = -1,
                    EndLineIndex = -1
                };
            }
        }

        public static void AdjustBoundsToFitGrid(BrushData target, double stepX, double stepY, Action<string> log)
        {
            double newWidth = Math.Max(stepX, Math.Round(target.WidthX / stepX) * stepX);
            double newLength = Math.Max(stepY, Math.Round(target.LengthY / stepY) * stepY);

            if (Math.Abs(target.WidthX - newWidth) > 0.001 || Math.Abs(target.LengthY - newLength) > 0.001)
            {
                double diffX = newWidth - target.WidthX;
                double diffY = newLength - target.LengthY;

                target.MinX = Math.Round(target.MinX - (diffX / 2.0));
                target.MaxX = target.MinX + newWidth;
                target.MinY = Math.Round(target.MinY - (diffY / 2.0));
                target.MaxY = target.MinY + newLength;

                target.WidthX = newWidth;
                target.LengthY = newLength;
            }
        }

        public static Dictionary<(double x, double y), double> GenerateHeightMap(BrushData target, double stepX, double stepY, int shapeType, double shapeHeight, double variance, double frequency, int noiseType, double terraceStep)
        {
            var heightMap = new Dictionary<(double x, double y), double>();
            Random rnd = new Random();
            double seedX = rnd.NextDouble() * 10000;
            double seedY = rnd.NextDouble() * 10000;

            for (double x = target.MinX; x <= target.MaxX + 0.01; x += stepX)
            {
                for (double y = target.MinY; y <= target.MaxY + 0.01; y += stepY)
                {
                    double nx = target.WidthX > 0 ? (x - target.MinX) / target.WidthX : 0;
                    double ny = target.LengthY > 0 ? (y - target.MinY) / target.LengthY : 0;

                    double baseShapeZ = 0;
                    double centerDist = Math.Min(1.0, Math.Sqrt(Math.Pow(nx - 0.5, 2) + Math.Pow(ny - 0.5, 2)) / 0.5);

                    if (shapeType == 1) baseShapeZ = shapeHeight * 0.5 * (1.0 + Math.Cos(centerDist * Math.PI));
                    else if (shapeType == 2) baseShapeZ = shapeHeight * 0.5 * (1.0 - Math.Cos(centerDist * Math.PI));
                    else if (shapeType == 3) { double dist = Math.Min(1.0, Math.Abs(nx - 0.5) / 0.5); baseShapeZ = shapeHeight * 0.5 * (1.0 + Math.Cos(dist * Math.PI)); }
                    else if (shapeType == 4) baseShapeZ = shapeHeight * nx;
                    else if (shapeType == 5)
                    {
                        double mountain = shapeHeight * 0.5 * (1.0 + Math.Cos(centerDist * Math.PI));
                        double craterDist = Math.Min(1.0, centerDist / 0.35);
                        double crater = (shapeHeight * 0.7) * 0.5 * (1.0 + Math.Cos(craterDist * Math.PI));
                        baseShapeZ = mountain - crater;
                    }
                    else if (shapeType == 6) { double dist = Math.Min(1.0, Math.Abs(nx - 0.5) / 0.5); baseShapeZ = shapeHeight * 0.5 * (1.0 - Math.Cos(dist * Math.PI)); }

                    double noiseZ = 0;
                    if (variance > 0)
                    {
                        if (noiseType == 0) noiseZ = Perlin.Noise((x + seedX) * frequency, (y + seedY) * frequency) * variance;
                        else if (noiseType == 1) noiseZ = Simplex.Noise((x + seedX) * frequency, (y + seedY) * frequency) * variance;
                        else if (noiseType == 2) noiseZ = (rnd.NextDouble() * (variance * 2)) - variance;
                    }

                    double finalZ = target.MaxZ + baseShapeZ + noiseZ;
                    if (shapeType > 0 && terraceStep > 0) finalZ = Math.Floor(finalZ / terraceStep) * terraceStep;
                    heightMap[(Math.Round(x, 2), Math.Round(y, 2))] = finalZ;
                }
            }
            return heightMap;
        }

        public static (Dictionary<(double x, double y), double> floorMap,
            Dictionary<(double x, double y), double> ceilingMap,
            Dictionary<(double y, double z), double> leftWallMap,
            Dictionary<(double y, double z), double> rightWallMap,
            double stepZ)
            GenerateTunnelHeightMaps(BrushData target, double stepX, double stepY, double caveHeight, double slopeHeight, double variance, double frequency, int noiseType, double terraceStep)
        {
            var floorMap = new Dictionary<(double x, double y), double>();
            var ceilingMap = new Dictionary<(double x, double y), double>();
            var leftWallMap = new Dictionary<(double y, double z), double>();
            var rightWallMap = new Dictionary<(double y, double z), double>();
            Random rnd = new Random();
            double seedFloorX = rnd.NextDouble() * 10000, seedFloorY = rnd.NextDouble() * 10000;
            double seedCeilX = rnd.NextDouble() * 10000, seedCeilY = rnd.NextDouble() * 10000;
            double seedWallL = rnd.NextDouble() * 10000;
            double seedWallR = rnd.NextDouble() * 10000;

            double centerX = (target.MinX + target.MaxX) / 2.0;
            double halfWidth = target.WidthX / 2.0;

            // Generate floor and ceiling heightmaps
            for (double x = target.MinX; x <= target.MaxX + 0.01; x += stepX)
            {
                for (double y = target.MinY; y <= target.MaxY + 0.01; y += stepY)
                {
                    // Circular blend: 0 at center, 1 at edge
                    double t = halfWidth > 0 ? Math.Min(1.0, Math.Abs(x - centerX) / halfWidth) : 0;
                    double blend = 1.0 - Math.Sqrt(Math.Max(0, 1.0 - t * t));

                    double floorNoise = 0, ceilNoise = 0;
                    if (variance > 0)
                    {
                        if (noiseType == 0)
                        {
                            floorNoise = Math.Abs(Perlin.Noise((x + seedFloorX) * frequency, (y + seedFloorY) * frequency)) * variance;
                            ceilNoise = Math.Abs(Perlin.Noise((x + seedCeilX) * frequency, (y + seedCeilY) * frequency)) * variance;
                        }
                        else if (noiseType == 1)
                        {
                            floorNoise = Math.Abs(Simplex.Noise((x + seedFloorX) * frequency, (y + seedFloorY) * frequency)) * variance;
                            ceilNoise = Math.Abs(Simplex.Noise((x + seedCeilX) * frequency, (y + seedCeilY) * frequency)) * variance;
                        }
                        else if (noiseType == 2)
                        {
                            floorNoise = rnd.NextDouble() * variance;
                            ceilNoise = rnd.NextDouble() * variance;
                        }
                    }

                    double ny = target.LengthY > 0 ? (y - target.MinY) / target.LengthY : 0;
                    double baseZ = target.MaxZ + slopeHeight * ny;
                    double floorZ = baseZ + blend * (caveHeight * 0.25) + floorNoise;
                    double ceilZ = baseZ + caveHeight - blend * (caveHeight * 0.25) - ceilNoise;

                    if (floorZ > ceilZ) { double mid = (floorZ + ceilZ) / 2.0; floorZ = mid; ceilZ = mid; }

                    if (terraceStep > 0)
                    {
                        floorZ = Math.Floor(floorZ / terraceStep) * terraceStep;
                        ceilZ = Math.Ceiling(ceilZ / terraceStep) * terraceStep;
                    }

                    floorMap[(Math.Round(x, 2), Math.Round(y, 2))] = floorZ;
                    ceilingMap[(Math.Round(x, 2), Math.Round(y, 2))] = ceilZ;
                }
            }

            // Wall step in Z: close to stepX but evenly divides the full height range
            double totalWallHeight = caveHeight + slopeHeight;
            int numZSteps = Math.Max(1, (int)Math.Round(totalWallHeight / stepX));
            double stepZ = totalWallHeight / numZSteps;
            double wallMinZ = target.MaxZ;
            double wallMaxZ = target.MaxZ + totalWallHeight;

            // Generate 2D wall heightmaps over (Y, Z) grid — same pattern as floor, just rotated
            for (double y = target.MinY; y <= target.MaxY + 0.01; y += stepY)
            {
                for (double z = wallMinZ; z <= wallMaxZ + 0.01; z += stepZ)
                {
                    double ry = Math.Round(y, 2), rz = Math.Round(z, 2);

                    double wallNoise = 0;
                    if (variance > 0)
                    {
                        if (noiseType == 0) wallNoise = Math.Abs(Perlin.Noise((y + seedWallL) * frequency, (z + seedWallL) * frequency)) * variance;
                        else if (noiseType == 1) wallNoise = Math.Abs(Simplex.Noise((y + seedWallL) * frequency, (z + seedWallL) * frequency)) * variance;
                        else if (noiseType == 2) wallNoise = rnd.NextDouble() * variance;
                    }
                    leftWallMap[(ry, rz)] = target.MinX + wallNoise;

                    wallNoise = 0;
                    if (variance > 0)
                    {
                        if (noiseType == 0) wallNoise = Math.Abs(Perlin.Noise((y + seedWallR) * frequency, (z + seedWallR) * frequency)) * variance;
                        else if (noiseType == 1) wallNoise = Math.Abs(Simplex.Noise((y + seedWallR) * frequency, (z + seedWallR) * frequency)) * variance;
                        else if (noiseType == 2) wallNoise = rnd.NextDouble() * variance;
                    }
                    rightWallMap[(ry, rz)] = target.MaxX - wallNoise;
                }
            }

            return (floorMap, ceilingMap, leftWallMap, rightWallMap, stepZ);
        }

        public static string GenerateTunnelFuncGroups(BrushData originalBrush, double stepX, double stepY, double stepZ, string topTexture,
            Dictionary<(double, double), double> floorMap, Dictionary<(double, double), double> ceilingMap,
            Dictionary<(double y, double z), double> leftWallMap, Dictionary<(double y, double z), double> rightWallMap, double caveHeight, double slopeHeight = 0)
        {
            StringBuilder sbFloor = new StringBuilder();
            StringBuilder sbCeiling = new StringBuilder();
            StringBuilder sbLeftWall = new StringBuilder();
            StringBuilder sbRightWall = new StringBuilder();

            sbFloor.AppendLine("// entity"); sbFloor.AppendLine("{"); sbFloor.AppendLine("\"classname\" \"func_group\"");
            sbCeiling.AppendLine("// entity"); sbCeiling.AppendLine("{"); sbCeiling.AppendLine("\"classname\" \"func_group\"");
            sbLeftWall.AppendLine("// entity"); sbLeftWall.AppendLine("{"); sbLeftWall.AppendLine("\"classname\" \"func_group\"");
            sbRightWall.AppendLine("// entity"); sbRightWall.AppendLine("{"); sbRightWall.AppendLine("\"classname\" \"func_group\"");

            int fBrush = 0, cBrush = 0, lwBrush = 0, rwBrush = 0;
            double minZ = originalBrush.MinZ;
            double maxCeilZ = originalBrush.MaxZ + caveHeight + slopeHeight;
            string caulkTex = "common/caulk 0 0 0";
            string topTex = $"{topTexture} 0 0 0";
            string matrix = "( ( 0.03125 0 0 ) ( 0 0.03125 0 ) )";

            // Floor and ceiling brushes
            for (double x = originalBrush.MinX; x < originalBrush.MaxX - 0.01; x += stepX)
            {
                for (double y = originalBrush.MinY; y < originalBrush.MaxY - 0.01; y += stepY)
                {
                    double currentMaxX = Math.Min(x + stepX, originalBrush.MaxX);
                    double currentMaxY = Math.Min(y + stepY, originalBrush.MaxY);

                    double fBL = floorMap[(Math.Round(x, 2), Math.Round(y, 2))];
                    double fTL = floorMap[(Math.Round(x, 2), Math.Round(currentMaxY, 2))];
                    double fBR = floorMap[(Math.Round(currentMaxX, 2), Math.Round(y, 2))];
                    double fTR = floorMap[(Math.Round(currentMaxX, 2), Math.Round(currentMaxY, 2))];

                    double cBL = ceilingMap[(Math.Round(x, 2), Math.Round(y, 2))];
                    double cTL = ceilingMap[(Math.Round(x, 2), Math.Round(currentMaxY, 2))];
                    double cBR = ceilingMap[(Math.Round(currentMaxX, 2), Math.Round(y, 2))];
                    double cTR = ceilingMap[(Math.Round(currentMaxX, 2), Math.Round(currentMaxY, 2))];

                    // Floor triangle 1
                    sbFloor.AppendLine($"// brush {fBrush++}\n{{\nbrushDef\n{{");
                    sbFloor.AppendLine($"( {x} {y} {fBL} ) ( {x} {currentMaxY} {fTL} ) ( {currentMaxX} {y} {fBR} ) {matrix} {topTex}");
                    sbFloor.AppendLine($"( {x} {y} {minZ} ) ( {currentMaxX} {y} {minZ} ) ( {x} {currentMaxY} {minZ} ) {matrix} {caulkTex}");
                    sbFloor.AppendLine($"( {x} {y} {minZ} ) ( {x} {currentMaxY} {minZ} ) ( {x} {y} {maxCeilZ} ) {matrix} {caulkTex}");
                    sbFloor.AppendLine($"( {x} {y} {minZ} ) ( {x} {y} {maxCeilZ} ) ( {currentMaxX} {y} {minZ} ) {matrix} {caulkTex}");
                    sbFloor.AppendLine($"( {currentMaxX} {y} {minZ} ) ( {currentMaxX} {y} {maxCeilZ} ) ( {x} {currentMaxY} {minZ} ) {matrix} {caulkTex}");
                    sbFloor.AppendLine("}\n}");

                    // Floor triangle 2
                    sbFloor.AppendLine($"// brush {fBrush++}\n{{\nbrushDef\n{{");
                    sbFloor.AppendLine($"( {currentMaxX} {currentMaxY} {fTR} ) ( {currentMaxX} {y} {fBR} ) ( {x} {currentMaxY} {fTL} ) {matrix} {topTex}");
                    sbFloor.AppendLine($"( {currentMaxX} {currentMaxY} {minZ} ) ( {x} {currentMaxY} {minZ} ) ( {currentMaxX} {y} {minZ} ) {matrix} {caulkTex}");
                    sbFloor.AppendLine($"( {currentMaxX} {y} {minZ} ) ( {currentMaxX} {y} {maxCeilZ} ) ( {currentMaxX} {currentMaxY} {minZ} ) {matrix} {caulkTex}");
                    sbFloor.AppendLine($"( {x} {currentMaxY} {minZ} ) ( {currentMaxX} {currentMaxY} {minZ} ) ( {x} {currentMaxY} {maxCeilZ} ) {matrix} {caulkTex}");
                    sbFloor.AppendLine($"( {x} {currentMaxY} {minZ} ) ( {x} {currentMaxY} {maxCeilZ} ) ( {currentMaxX} {y} {minZ} ) {matrix} {caulkTex}");
                    sbFloor.AppendLine("}\n}");

                    // Ceiling triangle 1 (reversed floor: solid above, textured face visible from below)
                    sbCeiling.AppendLine($"// brush {cBrush++}\n{{\nbrushDef\n{{");
                    sbCeiling.AppendLine($"( {x} {y} {cBL} ) ( {currentMaxX} {y} {cBR} ) ( {x} {currentMaxY} {cTL} ) {matrix} {topTex}");
                    sbCeiling.AppendLine($"( {x} {y} {maxCeilZ} ) ( {x} {currentMaxY} {maxCeilZ} ) ( {currentMaxX} {y} {maxCeilZ} ) {matrix} {caulkTex}");
                    sbCeiling.AppendLine($"( {x} {y} {minZ} ) ( {x} {currentMaxY} {minZ} ) ( {x} {y} {maxCeilZ} ) {matrix} {caulkTex}");
                    sbCeiling.AppendLine($"( {x} {y} {minZ} ) ( {x} {y} {maxCeilZ} ) ( {currentMaxX} {y} {minZ} ) {matrix} {caulkTex}");
                    sbCeiling.AppendLine($"( {currentMaxX} {y} {minZ} ) ( {currentMaxX} {y} {maxCeilZ} ) ( {x} {currentMaxY} {minZ} ) {matrix} {caulkTex}");
                    sbCeiling.AppendLine("}\n}");

                    // Ceiling triangle 2 (reversed floor)
                    sbCeiling.AppendLine($"// brush {cBrush++}\n{{\nbrushDef\n{{");
                    sbCeiling.AppendLine($"( {currentMaxX} {currentMaxY} {cTR} ) ( {x} {currentMaxY} {cTL} ) ( {currentMaxX} {y} {cBR} ) {matrix} {topTex}");
                    sbCeiling.AppendLine($"( {currentMaxX} {currentMaxY} {maxCeilZ} ) ( {currentMaxX} {y} {maxCeilZ} ) ( {x} {currentMaxY} {maxCeilZ} ) {matrix} {caulkTex}");
                    sbCeiling.AppendLine($"( {currentMaxX} {y} {minZ} ) ( {currentMaxX} {y} {maxCeilZ} ) ( {currentMaxX} {currentMaxY} {minZ} ) {matrix} {caulkTex}");
                    sbCeiling.AppendLine($"( {x} {currentMaxY} {minZ} ) ( {currentMaxX} {currentMaxY} {minZ} ) ( {x} {currentMaxY} {maxCeilZ} ) {matrix} {caulkTex}");
                    sbCeiling.AppendLine($"( {x} {currentMaxY} {minZ} ) ( {x} {currentMaxY} {maxCeilZ} ) ( {currentMaxX} {y} {minZ} ) {matrix} {caulkTex}");
                    sbCeiling.AppendLine("}\n}");
                }
            }

            // Left wall — same split-diagonal pattern as floor, rotated:
            //   grid axes: Y (first) and Z (second), displacement axis: X (inward from MinX)
            //   flat outer face at wallOuterLeft, sloped inner surface from leftWallMap
            double wallOuterLeft = originalBrush.MinX - stepX;
            double wallMinZ = originalBrush.MaxZ;
            double wallMaxZ = originalBrush.MaxZ + caveHeight + slopeHeight;
            double xLimitL = (originalBrush.MinX + originalBrush.MaxX) / 2.0; // mid-tunnel, for side plane extents

            for (double gy = originalBrush.MinY; gy < originalBrush.MaxY - 0.01; gy += stepY)
            {
                for (double gz = wallMinZ; gz < wallMaxZ - 0.01; gz += stepZ)
                {
                    double gmaxY = Math.Min(gy + stepY, originalBrush.MaxY);
                    double gmaxZ = Math.Min(gz + stepZ, wallMaxZ);
                    double rgy = Math.Round(gy, 2), rgmaxY = Math.Round(gmaxY, 2);
                    double rgz = Math.Round(gz, 2), rgmaxZ = Math.Round(gmaxZ, 2);

                    double xBL = leftWallMap[(rgy, rgz)];
                    double xTL = leftWallMap[(rgy, rgmaxZ)];
                    double xBR = leftWallMap[(rgmaxY, rgz)];
                    double xTR = leftWallMap[(rgmaxY, rgmaxZ)];

                    // Triangle 1: (BL, TL, BR) — inner normal -X
                    sbLeftWall.AppendLine($"// brush {lwBrush++}\n{{\nbrushDef\n{{");
                    sbLeftWall.AppendLine($"( {xBL} {gy} {gz} ) ( {xTL} {gy} {gmaxZ} ) ( {xBR} {gmaxY} {gz} ) {matrix} {topTex}");
                    sbLeftWall.AppendLine($"( {wallOuterLeft} {gy} {gz} ) ( {wallOuterLeft} {gmaxY} {gz} ) ( {wallOuterLeft} {gy} {gmaxZ} ) {matrix} {caulkTex}");
                    sbLeftWall.AppendLine($"( {wallOuterLeft} {gy} {gz} ) ( {wallOuterLeft} {gy} {gmaxZ} ) ( {xLimitL} {gy} {gz} ) {matrix} {caulkTex}");
                    sbLeftWall.AppendLine($"( {wallOuterLeft} {gy} {gz} ) ( {xLimitL} {gy} {gz} ) ( {wallOuterLeft} {gmaxY} {gz} ) {matrix} {caulkTex}");
                    sbLeftWall.AppendLine($"( {wallOuterLeft} {gmaxY} {gz} ) ( {xLimitL} {gmaxY} {gz} ) ( {wallOuterLeft} {gy} {gmaxZ} ) {matrix} {caulkTex}");
                    sbLeftWall.AppendLine("}\n}");

                    // Triangle 2: (TR, BR, TL) — inner normal -X
                    sbLeftWall.AppendLine($"// brush {lwBrush++}\n{{\nbrushDef\n{{");
                    sbLeftWall.AppendLine($"( {xTR} {gmaxY} {gmaxZ} ) ( {xBR} {gmaxY} {gz} ) ( {xTL} {gy} {gmaxZ} ) {matrix} {topTex}");
                    sbLeftWall.AppendLine($"( {wallOuterLeft} {gmaxY} {gmaxZ} ) ( {wallOuterLeft} {gy} {gmaxZ} ) ( {wallOuterLeft} {gmaxY} {gz} ) {matrix} {caulkTex}");
                    sbLeftWall.AppendLine($"( {wallOuterLeft} {gmaxY} {gz} ) ( {xLimitL} {gmaxY} {gz} ) ( {wallOuterLeft} {gmaxY} {gmaxZ} ) {matrix} {caulkTex}");
                    sbLeftWall.AppendLine($"( {wallOuterLeft} {gy} {gmaxZ} ) ( {wallOuterLeft} {gmaxY} {gmaxZ} ) ( {xLimitL} {gy} {gmaxZ} ) {matrix} {caulkTex}");
                    sbLeftWall.AppendLine($"( {wallOuterLeft} {gy} {gmaxZ} ) ( {xLimitL} {gy} {gmaxZ} ) ( {wallOuterLeft} {gmaxY} {gz} ) {matrix} {caulkTex}");
                    sbLeftWall.AppendLine("}\n}");
                }
            }

            // Right wall — same pattern, mirrored: displacement -X from MaxX, flat outer face at wallOuterRight
            double wallOuterRight = originalBrush.MaxX + stepX;
            double xLimitR = (originalBrush.MinX + originalBrush.MaxX) / 2.0;

            for (double gy = originalBrush.MinY; gy < originalBrush.MaxY - 0.01; gy += stepY)
            {
                for (double gz = wallMinZ; gz < wallMaxZ - 0.01; gz += stepZ)
                {
                    double gmaxY = Math.Min(gy + stepY, originalBrush.MaxY);
                    double gmaxZ = Math.Min(gz + stepZ, wallMaxZ);
                    double rgy = Math.Round(gy, 2), rgmaxY = Math.Round(gmaxY, 2);
                    double rgz = Math.Round(gz, 2), rgmaxZ = Math.Round(gmaxZ, 2);

                    double xBL = rightWallMap[(rgy, rgz)];
                    double xTL = rightWallMap[(rgy, rgmaxZ)];
                    double xBR = rightWallMap[(rgmaxY, rgz)];
                    double xTR = rightWallMap[(rgmaxY, rgmaxZ)];

                    // Triangle 1: inner normal +X (surface faces into tunnel from the right)
                    sbRightWall.AppendLine($"// brush {rwBrush++}\n{{\nbrushDef\n{{");
                    sbRightWall.AppendLine($"( {xBL} {gy} {gz} ) ( {xBR} {gmaxY} {gz} ) ( {xTL} {gy} {gmaxZ} ) {matrix} {topTex}");
                    sbRightWall.AppendLine($"( {wallOuterRight} {gy} {gz} ) ( {wallOuterRight} {gy} {gmaxZ} ) ( {wallOuterRight} {gmaxY} {gz} ) {matrix} {caulkTex}");
                    sbRightWall.AppendLine($"( {wallOuterRight} {gy} {gz} ) ( {xLimitR} {gy} {gz} ) ( {wallOuterRight} {gy} {gmaxZ} ) {matrix} {caulkTex}");
                    sbRightWall.AppendLine($"( {wallOuterRight} {gy} {gz} ) ( {wallOuterRight} {gmaxY} {gz} ) ( {xLimitR} {gy} {gz} ) {matrix} {caulkTex}");
                    sbRightWall.AppendLine($"( {wallOuterRight} {gmaxY} {gz} ) ( {wallOuterRight} {gy} {gmaxZ} ) ( {xLimitR} {gmaxY} {gz} ) {matrix} {caulkTex}");
                    sbRightWall.AppendLine("}\n}");

                    // Triangle 2: inner normal +X
                    sbRightWall.AppendLine($"// brush {rwBrush++}\n{{\nbrushDef\n{{");
                    sbRightWall.AppendLine($"( {xTR} {gmaxY} {gmaxZ} ) ( {xTL} {gy} {gmaxZ} ) ( {xBR} {gmaxY} {gz} ) {matrix} {topTex}");
                    sbRightWall.AppendLine($"( {wallOuterRight} {gmaxY} {gmaxZ} ) ( {wallOuterRight} {gmaxY} {gz} ) ( {wallOuterRight} {gy} {gmaxZ} ) {matrix} {caulkTex}");
                    sbRightWall.AppendLine($"( {wallOuterRight} {gmaxY} {gz} ) ( {wallOuterRight} {gmaxY} {gmaxZ} ) ( {xLimitR} {gmaxY} {gz} ) {matrix} {caulkTex}");
                    sbRightWall.AppendLine($"( {wallOuterRight} {gy} {gmaxZ} ) ( {xLimitR} {gy} {gmaxZ} ) ( {wallOuterRight} {gmaxY} {gmaxZ} ) {matrix} {caulkTex}");
                    sbRightWall.AppendLine($"( {wallOuterRight} {gy} {gmaxZ} ) ( {wallOuterRight} {gmaxY} {gz} ) ( {xLimitR} {gy} {gmaxZ} ) {matrix} {caulkTex}");
                    sbRightWall.AppendLine("}\n}");
                }
            }

            sbFloor.AppendLine("}");
            sbCeiling.AppendLine("}");
            sbLeftWall.AppendLine("}");
            sbRightWall.AppendLine("}");

            return sbFloor.ToString().TrimEnd() + "\n" +
                   sbCeiling.ToString().TrimEnd() + "\n" +
                   sbLeftWall.ToString().TrimEnd() + "\n" +
                   sbRightWall.ToString().TrimEnd();
        }

        public static string GenerateFuncGroup(BrushData originalBrush, double stepX, double stepY, bool splitDiagonally, string topTexture, Dictionary<(double, double), double> heightMap)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("// entity"); sb.AppendLine("{"); sb.AppendLine("\"classname\" \"func_group\"");

            int brushCount = 0;
            double minZ = originalBrush.MinZ;
            double baseMaxZ = originalBrush.MaxZ;
            string caulkTex = "common/caulk 0 0 0";
            string topTex = $"{topTexture} 0 0 0";
            string matrix = "( ( 0.03125 0 0 ) ( 0 0.03125 0 ) )";

            for (double x = originalBrush.MinX; x < originalBrush.MaxX - 0.01; x += stepX)
            {
                for (double y = originalBrush.MinY; y < originalBrush.MaxY - 0.01; y += stepY)
                {
                    double currentMaxX = Math.Min(x + stepX, originalBrush.MaxX);
                    double currentMaxY = Math.Min(y + stepY, originalBrush.MaxY);

                    double zBL = heightMap[(Math.Round(x, 2), Math.Round(y, 2))];
                    double zTL = heightMap[(Math.Round(x, 2), Math.Round(currentMaxY, 2))];
                    double zBR = heightMap[(Math.Round(currentMaxX, 2), Math.Round(y, 2))];
                    double zTR = heightMap[(Math.Round(currentMaxX, 2), Math.Round(currentMaxY, 2))];

                    if (!splitDiagonally)
                    {
                        sb.AppendLine($"// brush {brushCount++}\n{{\nbrushDef\n{{");
                        sb.AppendLine($"( {x} {y} {baseMaxZ} ) ( {x} {currentMaxY} {baseMaxZ} ) ( {currentMaxX} {y} {baseMaxZ} ) {matrix} {topTex}");
                        sb.AppendLine($"( {x} {y} {minZ} ) ( {currentMaxX} {y} {minZ} ) ( {x} {currentMaxY} {minZ} ) {matrix} {caulkTex}");
                        sb.AppendLine($"( {currentMaxX} {y} {minZ} ) ( {currentMaxX} {y} {baseMaxZ} ) ( {currentMaxX} {currentMaxY} {minZ} ) {matrix} {caulkTex}");
                        sb.AppendLine($"( {x} {y} {minZ} ) ( {x} {currentMaxY} {minZ} ) ( {x} {y} {baseMaxZ} ) {matrix} {caulkTex}");
                        sb.AppendLine($"( {x} {currentMaxY} {minZ} ) ( {currentMaxX} {currentMaxY} {minZ} ) ( {x} {currentMaxY} {baseMaxZ} ) {matrix} {caulkTex}");
                        sb.AppendLine($"( {x} {y} {minZ} ) ( {x} {y} {baseMaxZ} ) ( {currentMaxX} {y} {minZ} ) {matrix} {caulkTex}");
                        sb.AppendLine("}\n}");
                    }
                    else
                    {
                        sb.AppendLine($"// brush {brushCount++}\n{{\nbrushDef\n{{");
                        sb.AppendLine($"( {x} {y} {zBL} ) ( {x} {currentMaxY} {zTL} ) ( {currentMaxX} {y} {zBR} ) {matrix} {topTex}");
                        sb.AppendLine($"( {x} {y} {minZ} ) ( {currentMaxX} {y} {minZ} ) ( {x} {currentMaxY} {minZ} ) {matrix} {caulkTex}");
                        sb.AppendLine($"( {x} {y} {minZ} ) ( {x} {currentMaxY} {minZ} ) ( {x} {y} {baseMaxZ} ) {matrix} {caulkTex}");
                        sb.AppendLine($"( {x} {y} {minZ} ) ( {x} {y} {baseMaxZ} ) ( {currentMaxX} {y} {minZ} ) {matrix} {caulkTex}");
                        sb.AppendLine($"( {currentMaxX} {y} {minZ} ) ( {currentMaxX} {y} {baseMaxZ} ) ( {x} {currentMaxY} {minZ} ) {matrix} {caulkTex}");
                        sb.AppendLine("}\n}");

                        sb.AppendLine($"// brush {brushCount++}\n{{\nbrushDef\n{{");
                        sb.AppendLine($"( {currentMaxX} {currentMaxY} {zTR} ) ( {currentMaxX} {y} {zBR} ) ( {x} {currentMaxY} {zTL} ) {matrix} {topTex}");
                        sb.AppendLine($"( {currentMaxX} {currentMaxY} {minZ} ) ( {x} {currentMaxY} {minZ} ) ( {currentMaxX} {y} {minZ} ) {matrix} {caulkTex}");
                        sb.AppendLine($"( {currentMaxX} {y} {minZ} ) ( {currentMaxX} {y} {baseMaxZ} ) ( {currentMaxX} {currentMaxY} {minZ} ) {matrix} {caulkTex}");
                        sb.AppendLine($"( {x} {currentMaxY} {minZ} ) ( {currentMaxX} {currentMaxY} {minZ} ) ( {x} {currentMaxY} {baseMaxZ} ) {matrix} {caulkTex}");
                        sb.AppendLine($"( {x} {currentMaxY} {minZ} ) ( {x} {currentMaxY} {baseMaxZ} ) ( {currentMaxX} {y} {minZ} ) {matrix} {caulkTex}");
                        sb.AppendLine("}\n}");
                    }
                }
            }
            sb.AppendLine("}");
            return sb.ToString().TrimEnd();
        }

        public static bool ExportFile(
         string mode,
         string[] originalLines,
         BrushData target,
         string newFuncGroup,
         string sourceFilePath,
         string outName,
         bool overrideMap,
         string configOutputFolder, 
         Action<string> log)       
            {
            try
            {
                List<string> newFileLines = new List<string>();
                string outputFilePath = "";
                string baseFolder = string.IsNullOrWhiteSpace(configOutputFolder)
                    ? AppDomain.CurrentDomain.BaseDirectory
                    : configOutputFolder;

                if (mode.ToLower() == "hint" && target.StartLineIndex != -1)
                {
                    if (overrideMap)
                    {
                        outputFilePath = sourceFilePath;
                    }
                    else
                    {
                        string fileName = string.IsNullOrWhiteSpace(outName) ? "output_" + Path.GetFileName(sourceFilePath) : outName;
                        if (!fileName.EndsWith(".map", StringComparison.OrdinalIgnoreCase)) fileName += ".map";
                        outputFilePath = Path.Combine(Path.GetDirectoryName(sourceFilePath), fileName);
                    }

                    newFileLines.AddRange(originalLines.Take(target.StartLineIndex));
                    newFileLines.Add(newFuncGroup);
                    newFileLines.AddRange(originalLines.Skip(target.EndLineIndex + 1));
                }
                else
                {
                    // Manual Mode or New File
                    string fileName = string.IsNullOrWhiteSpace(outName) ? "terrain_output.map" : outName;
                    if (!fileName.EndsWith(".map", StringComparison.OrdinalIgnoreCase)) fileName += ".map";

                    outputFilePath = Path.Combine(baseFolder, fileName);

                    newFileLines.Add("// entity 0\n{\n\"classname\" \"worldspawn\"\n}");
                    newFileLines.Add("\n" + newFuncGroup);
                }

                File.WriteAllLines(outputFilePath, newFileLines);
                log($"SUCCESS! Map saved to:\n{outputFilePath}");
                return true;
            }
            catch (Exception ex)
            {
                log($"Error exporting map: {ex.Message}");
                return false;
            }
        }

        // --- INTERNAL PARSING & CSG ---
        private static BrushData FindValidHintBrush(string[] lines)
        {
            bool inFuncGroup = false; int brushCount = 0; bool inBrushDef = false;
            int funcGroupStartIndex = -1; List<string> currentBrushLines = new List<string>();

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (line == "\"classname\" \"func_group\"") { inFuncGroup = true; brushCount = 0; funcGroupStartIndex = i >= 2 ? i - 2 : i; continue; }
                if (line == "// entity" || (line.StartsWith("// entity") && inFuncGroup) || (i == lines.Length - 1 && inFuncGroup))
                {
                    if (inFuncGroup && brushCount == 1)
                    {
                        var data = ParseBrush(currentBrushLines);
                        if (data != null) { data.StartLineIndex = funcGroupStartIndex; data.EndLineIndex = i == lines.Length - 1 ? i : i - 1; return data; }
                    }
                    inFuncGroup = false;
                }
                if (inFuncGroup)
                {
                    if (line == "brushDef") { brushCount++; inBrushDef = true; currentBrushLines.Clear(); }
                    else if (inBrushDef) { if (line == "}") inBrushDef = false; else if (line.StartsWith("(")) currentBrushLines.Add(line); }
                }
            }
            return null;
        }

        private static BrushData ParseBrush(List<string> brushLines)
        {
            BrushData data = new BrushData(); List<Plane> planes = new List<Plane>();
            Regex coordRegex = new Regex(@"\(\s*([-\d.]+)\s+([-\d.]+)\s+([-\d.]+)\s*\)");

            foreach (var line in brushLines)
            {
                if (!line.Contains("common/hint")) return null;
                var matches = coordRegex.Matches(line);
                if (matches.Count >= 3)
                {
                    Vector3 p1 = new Vector3(double.Parse(matches[0].Groups[1].Value), double.Parse(matches[0].Groups[2].Value), double.Parse(matches[0].Groups[3].Value));
                    Vector3 p2 = new Vector3(double.Parse(matches[1].Groups[1].Value), double.Parse(matches[1].Groups[2].Value), double.Parse(matches[1].Groups[3].Value));
                    Vector3 p3 = new Vector3(double.Parse(matches[2].Groups[1].Value), double.Parse(matches[2].Groups[2].Value), double.Parse(matches[2].Groups[3].Value));
                    planes.Add(new Plane(p1, p2, p3));
                }
            }

            List<Vector3> validVertices = new List<Vector3>();
            for (int i = 0; i < planes.Count; i++)
                for (int j = i + 1; j < planes.Count; j++)
                    for (int k = j + 1; k < planes.Count; k++)
                        if (Plane.TryGetIntersection(planes[i], planes[j], planes[k], out Vector3 intersection))
                        {
                            bool isValid = true;
                            foreach (var plane in planes) if (plane.DistanceToPoint(intersection) > 0.01) { isValid = false; break; }
                            if (isValid) validVertices.Add(intersection);
                        }

            if (validVertices.Count == 0) return null;

            data.MinX = Math.Round(validVertices.Min(v => v.X)); data.MaxX = Math.Round(validVertices.Max(v => v.X));
            data.MinY = Math.Round(validVertices.Min(v => v.Y)); data.MaxY = Math.Round(validVertices.Max(v => v.Y));
            data.MinZ = Math.Round(validVertices.Min(v => v.Z)); data.MaxZ = Math.Round(validVertices.Max(v => v.Z));
            data.WidthX = data.MaxX - data.MinX; data.LengthY = data.MaxY - data.MinY; data.HeightZ = data.MaxZ - data.MinZ;
            return data;
        }
    }

    // --- PERLIN NOISE ---
    public static class Perlin
    {
        private static readonly int[] P = new int[512];
        private static readonly int[] permutation = { 151,160,137,91,90,15,131,13,201,95,96,53,194,233,7,225,140,36,103,30,69,142,8,99,37,240,21,10,23,
            190, 6,148,247,120,234,75,0,26,197,62,94,252,219,203,117,35,11,32,57,177,33,88,237,149,56,87,174,20,125,136,171,168, 68,175,74,165,71,134,139,48,27,166,
            77,146,158,231,83,111,229,122,60,211,133,230,220,105,92,41,55,46,245,40,244,102,143,54, 65,25,63,161, 1,216,80,73,209,76,132,187,208, 89,18,169,200,196,
            135,130,116,188,159,86,164,100,109,198,173,186, 3,64,52,217,226,250,124,123,5,202,38,147,118,126,255,82,85,212,207,206,59,227,47,16,58,17,182,189,28,42,
            223,183,170,213,119,248,152, 2,44,154,163, 70,221,153,101,155,167, 43,172,9,129,22,39,253, 19,98,108,110,79,113,224,232,178,185, 112,104,218,246,97,228,
            251,34,242,193,238,210,144,12,191,179,162,241, 81,51,145,235,249,14,239,107,49,192,214, 31,181,199,106,157,184, 84,204,176,115,121,50,45,127, 4,150,254,
            138,236,205,93,222,114,67,29,24,72,243,141,128,195,78,66,215,61,156,180
        };

        static Perlin() { for (int i = 0; i < 256; i++) P[i] = P[i + 256] = permutation[i]; }

        public static double Noise(double x, double y)
        {
            int X = (int)Math.Floor(x) & 255, Y = (int)Math.Floor(y) & 255;
            x -= Math.Floor(x); y -= Math.Floor(y);
            double u = Fade(x), v = Fade(y);
            int A = P[X] + Y, B = P[X + 1] + Y;
            return Lerp(v, Lerp(u, Grad(P[A], x, y), Grad(P[B], x - 1, y)), Lerp(u, Grad(P[A + 1], x, y - 1), Grad(P[B + 1], x - 1, y - 1)));
        }
        static double Fade(double t) => t * t * t * (t * (t * 6 - 15) + 10);
        static double Lerp(double t, double a, double b) => a + t * (b - a);
        static double Grad(int hash, double x, double y)
        {
            int h = hash & 15;
            double u = h < 8 ? x : y, v = h < 4 ? y : h == 12 || h == 14 ? x : 0;
            return ((h & 1) == 0 ? u : -u) + ((h & 2) == 0 ? v : -v);
        }
    }

    // --- SIMPLEX NOISE ---
    public static class Simplex
    {
        private static readonly int[] perm = new int[512];
        private static readonly int[] p = { 
            151,160,137,91,90,15,131,13,201,95,96,53,194,233,7,225,140,36,103,30,69,142,8,99,37,240,21,10,23,
            190, 6,148,247,120,234,75,0,26,197,62,94,252,219,203,117,35,11,32,57,177,33,88,237,149,56,87,174,20,125,136,171,168, 68,175,74,165,71,134,139,48,27,166,
            77,146,158,231,83,111,229,122,60,211,133,230,220,105,92,41,55,46,245,40,244,102,143,54, 65,25,63,161, 1,216,80,73,209,76,132,187,208, 89,18,169,200,196,
            135,130,116,188,159,86,164,100,109,198,173,186, 3,64,52,217,226,250,124,123,5,202,38,147,118,126,255,82,85,212,207,206,59,227,47,16,58,17,182,189,28,42,
            223,183,170,213,119,248,152, 2,44,154,163, 70,221,153,101,155,167, 43,172,9,129,22,39,253, 19,98,108,110,79,113,224,232,178,185, 112,104,218,246,97,228,
            251,34,242,193,238,210,144,12,191,179,162,241, 81,51,145,235,249,14,239,107,49,192,214, 31,181,199,106,157,184, 84,204,176,115,121,50,45,127, 4,150,254,
            138,236,205,93,222,114,67,29,24,72,243,141,128,195,78,66,215,61,156,180
        };

        static Simplex() { for (int i = 0; i < 256; i++) perm[i] = perm[i + 256] = p[i]; }

        public static double Noise(double x, double y)
        {
            double F2 = 0.5 * (Math.Sqrt(3.0) - 1.0);
            double s = (x + y) * F2;
            int i = FastFloor(x + s);
            int j = FastFloor(y + s);

            double G2 = (3.0 - Math.Sqrt(3.0)) / 6.0;
            double t = (i + j) * G2;
            double X0 = i - t;
            double Y0 = j - t;
            double x0 = x - X0;
            double y0 = y - Y0;

            int i1, j1;
            if (x0 > y0) { i1 = 1; j1 = 0; } else { i1 = 0; j1 = 1; }

            double x1 = x0 - i1 + G2;
            double y1 = y0 - j1 + G2;
            double x2 = x0 - 1.0 + 2.0 * G2;
            double y2 = y0 - 1.0 + 2.0 * G2;

            int ii = i & 255;
            int jj = j & 255;
            int gi0 = perm[ii + perm[jj]] % 12;
            int gi1 = perm[ii + i1 + perm[jj + j1]] % 12;
            int gi2 = perm[ii + 1 + perm[jj + 1]] % 12;

            double t0 = 0.5 - x0 * x0 - y0 * y0;
            double n0 = t0 < 0 ? 0.0 : Math.Pow(t0, 4) * Dot(Grad3[gi0], x0, y0);

            double t1 = 0.5 - x1 * x1 - y1 * y1;
            double n1 = t1 < 0 ? 0.0 : Math.Pow(t1, 4) * Dot(Grad3[gi1], x1, y1);

            double t2 = 0.5 - x2 * x2 - y2 * y2;
            double n2 = t2 < 0 ? 0.0 : Math.Pow(t2, 4) * Dot(Grad3[gi2], x2, y2);

            return 70.0 * (n0 + n1 + n2);
        }

        private static int FastFloor(double x) { int xi = (int)x; return x < xi ? xi - 1 : xi; }
        private static double Dot(int[] g, double x, double y) { return g[0] * x + g[1] * y; }

        private static readonly int[][] Grad3 = {
            new[]{1,1,0},new[]{-1,1,0},new[]{1,-1,0},new[]{-1,-1,0},
            new[]{1,0,1},new[]{-1,0,1},new[]{1,0,-1},new[]{-1,0,-1},
            new[]{0,1,1},new[]{0,-1,1},new[]{0,1,-1},new[]{0,-1,-1}
        };
    }

    public struct Vector3
    {
        public double X, Y, Z;
        public Vector3(double x, double y, double z) { X = x; Y = y; Z = z; }
        public static Vector3 operator -(Vector3 a, Vector3 b) => new Vector3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static Vector3 operator +(Vector3 a, Vector3 b) => new Vector3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static Vector3 operator *(Vector3 a, double d) => new Vector3(a.X * d, a.Y * d, a.Z * d);
        public static Vector3 operator /(Vector3 a, double d) => new Vector3(a.X / d, a.Y / d, a.Z / d);
        public static double Dot(Vector3 a, Vector3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;
        public static Vector3 Cross(Vector3 a, Vector3 b) => new Vector3(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);
    }

    public class Plane
    {
        public Vector3 Normal { get; }
        public double D { get; }
        public Plane(Vector3 p1, Vector3 p2, Vector3 p3)
        {
            Vector3 v1 = p1 - p2; Vector3 v2 = p3 - p2; Normal = Vector3.Cross(v1, v2);
            double length = Math.Sqrt(Vector3.Dot(Normal, Normal)); Normal = Normal / length;
            D = Vector3.Dot(Normal, p1);
        }
        public double DistanceToPoint(Vector3 p) { return Vector3.Dot(Normal, p) - D; }
        public static bool TryGetIntersection(Plane p1, Plane p2, Plane p3, out Vector3 intersection)
        {
            intersection = new Vector3(0, 0, 0);
            double det = Vector3.Dot(p1.Normal, Vector3.Cross(p2.Normal, p3.Normal));
            if (Math.Abs(det) < 0.0001) return false;
            Vector3 v1 = Vector3.Cross(p2.Normal, p3.Normal) * p1.D;
            Vector3 v2 = Vector3.Cross(p3.Normal, p1.Normal) * p2.D;
            Vector3 v3 = Vector3.Cross(p1.Normal, p2.Normal) * p3.D;
            intersection = (v1 + v2 + v3) / det;
            return true;
        }
    }

    public class BrushData
    {
        public int StartLineIndex { get; set; }
        public int EndLineIndex { get; set; }
        public double MinX { get; set; }
        public double MaxX { get; set; }
        public double MinY { get; set; }
        public double MaxY { get; set; }
        public double MinZ { get; set; }
        public double MaxZ { get; set; }
        public double WidthX { get; set; }
        public double LengthY { get; set; }
        public double HeightZ { get; set; }
    }
}