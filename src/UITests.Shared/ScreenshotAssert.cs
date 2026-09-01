using NUnit.Framework;
using OpenQA.Selenium.Appium;
using SkiaSharp;

namespace UITests;

// Compares a screenshot against a committed reference image.
//
// This is the only check that catches purely visual faults — a chart clipped to a sliver, or a
// ring flattened on one edge, still has the right element tree and the right text. It is also
// the most fragile kind of assertion: OS version, screen density, font rendering and animation
// timing all move pixels. The tolerances below are deliberately loose, baselines are stored per
// platform, and regenerating them is a documented one-liner rather than a chore.
public static class ScreenshotAssert
{
    // Per-channel difference below which two pixels count as equal. Absorbs anti-aliasing and
    // colour-management noise.
    private const int ChannelTolerance = 12;

    // Share of pixels allowed to differ before the comparison fails.
    private const double MaxDifferingPixelRatio = 0.005;   // 0.5%

    // Set UITEST_UPDATE_BASELINES=1 to write the current rendering as the new reference instead
    // of comparing against it.
    private static bool UpdateBaselines =>
        Environment.GetEnvironmentVariable("UITEST_UPDATE_BASELINES") == "1";

    public static void MatchesBaseline(AppiumDriver app, string name)
    {
        byte[] actualBytes = app.GetScreenshot().AsByteArray;

        string baselinePath = BaselinePath(name);
        Directory.CreateDirectory(Path.GetDirectoryName(baselinePath)!);

        if (UpdateBaselines || !File.Exists(baselinePath))
        {
            File.WriteAllBytes(baselinePath, actualBytes);
            string reason = UpdateBaselines ? "UITEST_UPDATE_BASELINES=1" : "no baseline existed";
            TestContext.WriteLine($"Wrote baseline {baselinePath} ({reason}).");
            Assert.That(
                UpdateBaselines,
                Is.True,
                $"No baseline for '{name}'. One has been written to {baselinePath}; " +
                "check it looks right and commit it, then re-run.");
            return;
        }

        using var actual = SKBitmap.Decode(actualBytes);
        using var expected = SKBitmap.Decode(File.ReadAllBytes(baselinePath));

        if (actual.Width != expected.Width || actual.Height != expected.Height)
        {
            SaveArtifacts(name, actualBytes, diff: null);
            Assert.Fail(
                $"Screenshot '{name}' is {actual.Width}x{actual.Height} but the baseline is " +
                $"{expected.Width}x{expected.Height}. A different device or density will do " +
                "this; regenerate the baselines if the change is expected.");
        }

        var (differingPixels, diffImage) = Compare(actual, expected);
        double ratio = (double)differingPixels / (actual.Width * actual.Height);

        using (diffImage)
        {
            if (ratio > MaxDifferingPixelRatio)
            {
                using var diffData = diffImage.Encode(SKEncodedImageFormat.Png, 100);
                SaveArtifacts(name, actualBytes, diffData.ToArray());
                Assert.Fail(
                    $"Screenshot '{name}' differs from its baseline in {ratio:P2} of pixels " +
                    $"(limit {MaxDifferingPixelRatio:P2}). The actual image and a diff have " +
                    "been attached to this test result.");
            }
        }

        TestContext.WriteLine($"Screenshot '{name}' matched its baseline ({ratio:P3} differing).");
    }

    private static (int DifferingPixels, SKBitmap Diff) Compare(SKBitmap actual, SKBitmap expected)
    {
        var diff = new SKBitmap(actual.Width, actual.Height);
        int differingPixels = 0;

        for (int y = 0; y < actual.Height; y++)
        {
            for (int x = 0; x < actual.Width; x++)
            {
                SKColor a = actual.GetPixel(x, y);
                SKColor e = expected.GetPixel(x, y);

                bool same =
                    Math.Abs(a.Red - e.Red) <= ChannelTolerance &&
                    Math.Abs(a.Green - e.Green) <= ChannelTolerance &&
                    Math.Abs(a.Blue - e.Blue) <= ChannelTolerance;

                if (same)
                {
                    // Keep matching areas visible but muted so the differences stand out.
                    byte grey = (byte)((a.Red + a.Green + a.Blue) / 3 / 3 + 170);
                    diff.SetPixel(x, y, new SKColor(grey, grey, grey));
                }
                else
                {
                    differingPixels++;
                    diff.SetPixel(x, y, SKColors.Magenta);
                }
            }
        }

        return (differingPixels, diff);
    }

    private static string BaselinePath(string name) =>
        Path.Combine(
            AppContext.BaseDirectory, "Baselines", AppiumSetup.PlatformName, $"{name}.png");

    private static void SaveArtifacts(string name, byte[] actual, byte[]? diff)
    {
        string directory = Path.Combine(AppContext.BaseDirectory, "ScreenshotFailures");
        Directory.CreateDirectory(directory);

        string actualPath = Path.Combine(directory, $"{name}.actual.png");
        File.WriteAllBytes(actualPath, actual);
        TestContext.AddTestAttachment(actualPath, $"{name} (actual)");

        if (diff is not null)
        {
            string diffPath = Path.Combine(directory, $"{name}.diff.png");
            File.WriteAllBytes(diffPath, diff);
            TestContext.AddTestAttachment(diffPath, $"{name} (diff)");
        }
    }
}
