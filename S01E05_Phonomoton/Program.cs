using HtmlAgilityPack;
using System;
using System.Globalization;
using System.Net.Http;
using System.Text.RegularExpressions;

namespace S01E05_Phonomoton
{
    internal static class Program
    {
        private static readonly HttpClient _httpClient = new HttpClient();

        private static int Main(string[] args)
        {
            Console.Write("URL of gsmarena page: ");
            var urlString = Console.ReadLine();

            // Example URLs for dev & test:
            // https://www.gsmarena.com/samsung_galaxy_tab_s3_9_7-8554.php
            // https://www.gsmarena.com/xiaomi_redmi_note_6_pro-9333.php
            // https://www.gsmarena.com/vivo_nex_dual_display-9435.php
            // https://www.gsmarena.com/nokia_110-4755.php

            if (!Uri.TryCreate(urlString, UriKind.Absolute, out var url))
            {
                Console.Error.WriteLine("That's not a valid URL.");
                return -1;
            }

            try
            {
                var pageBody = _httpClient.GetStringAsync(url).GetAwaiter().GetResult();

                var page = new HtmlDocument();
                page.LoadHtml(pageBody);

                var modelNameElement = page.DocumentNode.SelectSingleNode("//h1[@data-spec='modelname']");
                if (modelNameElement == null)
                    throw new NotSupportedException("No device info found on this page.");

                Console.WriteLine(modelNameElement.InnerText);

                double totalRawScore = 0;
                double totalRawWeight = 0;

                // Go through each aspect, evaluate the score against the ideal and aggregate the results.
                foreach (var aspect in _aspects)
                {
                    Console.WriteLine($"Evaluating {aspect.Name}");

                    totalRawWeight += aspect.Weight;

                    // Eact aspect is a number like "14.1" in the specs. We compare against the ideal number
                    // in order to determine the raw score of this aspect (raw means before weight is applied).
                    var element = page.DocumentNode.SelectSingleNode(AspectExpressions.SelfieCameraPixelsElement);
                    var extractedStrings = AspectExpressions.SelfieCameraPixelsExtractor.Match(element.InnerHtml);
                    var valueAsString = extractedStrings.Groups[1].Value;
                    var value = double.Parse(valueAsString, CultureInfo.InvariantCulture);

                    var aspectRawScore = value / aspect.Perfection;
                    Console.WriteLine($"\tRaw score {aspectRawScore:P2}");

                    totalRawScore += aspectRawScore;
                }

                // Finalize the score and apply weights.
                var score = totalRawScore / totalRawWeight;
                Console.WriteLine();
                Console.WriteLine($"This mobile device achieved a score of {score:P0}");

                return 0;
            }
            catch (Exception ex)
            {
                // Oh no! Something went wrong! Print the error and exit.
                Console.Error.WriteLine(ex);
                return -1;
            }
        }

        /// <summary>
        /// One aspect of the phone to evaluate. E.g. screen size.
        /// </summary>
        private sealed class Aspect
        {
            public string Name { get; }

            /// <summary>
            /// Some aspects are more important than others (affect the final score more).
            /// By default, the weight is 1 but this may be increased for more important aspects.
            /// </summary>
            public double Weight { get; set; } = 1;

            /// <summary>
            /// The value that must be achieved for this aspect to get a perfect score.
            /// Exceeding this value will not increase the score beyond 100%.
            /// Anything below this will reduce the score.
            /// </summary>
            public double Perfection { get; }

            public Aspect(string name, double perfection)
            {
                Name = name;
                Perfection = perfection;
            }
        }

        // Lists all the XPath and string parsing expressions we need to extract aspects from the GSMArena pages.
        private static class AspectExpressions
        {
            public const string ScreenSizeElement = "//td[@data-spec='displaysize']";
            public static readonly Regex ScreenSizeExtractor = new Regex(@"(\d+\.\d+) inches");

            public const string BatterySizeElement = "//td[@data-spec='batdescription1']";
            public static readonly Regex BatterySizeExtractor = new Regex(@"(\d+) mAh");

            public const string StorageSizeElement = "//td[@data-spec='internalmemory']";
            // This can be like "32 GB, 4 GB RAM" so be careful which one is used.
            public static readonly Regex StorageSizeExtractor = new Regex(@"(\d+) GB");

            public const string SelfieCameraPixelsElement = "//td[@data-spec='cam2modules']";
            public static readonly Regex SelfieCameraPixelsExtractor = new Regex(@"(\d+(\.\d+)?) MP");
        }

        private static readonly Aspect[] _aspects = new[]
        {
            new Aspect("Screen size", 5.5)
            {
                Weight = 3
            },
            new Aspect("Battery size", 3000),
            new Aspect("Storage size", 128),
            new Aspect("Selfie camera megapixels", 10)
            {
                Weight = 0.9
            }
        };
    }
}
