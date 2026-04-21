using Microsoft.Win32;
using Sdcb.WordClouds;
using SkiaSharp;
using System;
using System.Globalization;
using System.IO;
using System.Printing;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace WordCloud;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    #region DoEvent Helper
    private static object? ExitFrame(object state)
    {
        ((DispatcherFrame)state).Continue = false;
        return null;
    }

    private static readonly SemaphoreSlim CanDoEvents = new(1, 1);
    public static async void DoEvents()
    {
        if (await CanDoEvents.WaitAsync(0))
        {
            try
            {
                if (Application.Current?.Dispatcher?.CheckAccess() ?? false)
                {
                    await Dispatcher.Yield(DispatcherPriority.Normal);
                }
            }
            catch (Exception)
            {
                try
                {
                    if (Application.Current?.Dispatcher?.CheckAccess() ?? false)
                    {
                        DispatcherFrame frame = new();
                        await Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Send, new DispatcherOperationCallback(ExitFrame), frame);
                        Dispatcher.PushFrame(frame);
                    }
                }
                catch (Exception)
                {
                    //await Task.Delay(1);
                }
            }
            finally
            {
                //CanDoEvents.Release(max: 1);
                if (CanDoEvents?.CurrentCount <= 0) CanDoEvents?.Release();
            }
        }
    }
    #endregion

    #region Word Cloud Helper
    private byte[]? PngBytes { get; set; }

    public enum CloudBGStyle { None, White, Black, Color, Image };
    public enum CloudFGStyle { Color, Grayscale, BlackWhite };
    public enum CloudMaskStyle { None, ImageBW, ImageWB };

    public int CloudMargin
    {
        get => CloudMarginValue.Dispatcher.Invoke(() => Math.Max(0, int.Parse(CloudMarginValue.Text)));
        set => CloudMarginValue.Dispatcher.Invoke(() => CloudMarginValue.Text = $"{Math.Max(0, value)}");
    }
    public int CloudWidth
    {
        get => CloudWidthValue.Dispatcher.Invoke(() => Math.Max(100, int.Parse(CloudWidthValue.Text)));
        set => CloudWidthValue.Dispatcher.Invoke(() => CloudWidthValue.Text = $"{Math.Max(100, value)}");
    }
    public int CloudHeight
    {
        get => CloudHeightValue.Dispatcher.Invoke(() => Math.Max(100, int.Parse(CloudHeightValue.Text)));
        set => CloudHeightValue.Dispatcher.Invoke(() => CloudHeightValue.Text = $"{Math.Max(100, value)}");
    }

    public string CloudFontFamily
    {
        get => (CloudFontValue.Dispatcher.Invoke(() => CloudFontValue.Text)); 
        set => CloudFontValue.Dispatcher.Invoke(() => { CloudFontValue.Text = value; });
    }
    public TextOrientations CloudOrientation
    {
        get => (CloudOrientationValue.Dispatcher.Invoke(() => Enum.Parse<TextOrientations>(CloudOrientationValue.Text))); 
        set => CloudOrientationValue.Dispatcher.Invoke(() => { CloudOrientationValue.Text = value.ToString(); });
    }
    public CloudBGStyle CloudBG
    {
        get => (CloudBackgroundValue.Dispatcher.Invoke(() => Enum.Parse<CloudBGStyle>(CloudBackgroundValue.Text))); 
        set => CloudBackgroundValue.Dispatcher.Invoke(() => { CloudBackgroundValue.Text = value.ToString(); });
    }
    public CloudMaskStyle CloudMask
    {
        get => (CloudMaskImageValue.Dispatcher.Invoke(() => Enum.Parse<CloudMaskStyle>(CloudMaskImageValue.Text))); 
        set => CloudMaskImageValue.Dispatcher.Invoke(() => { CloudMaskImageValue.Text = value.ToString(); });
    }

    public string CloudWords
    {
        get => WordsTextBox.Dispatcher.Invoke(() => WordsTextBox.Text);
        set => WordsTextBox.Dispatcher.Invoke(() => WordsTextBox.Text = value);
    }

    public ImageSource WordCloudResult
    {
        get => WordCloudImage.Dispatcher.Invoke(() => WordCloudImage.Source); 
        set => WordCloudImage.Dispatcher.Invoke(() => WordCloudImage.Source = value);
    }

    private bool IsCloudBuilding
    {
        get => CloudBuildingIndicator?.Dispatcher?.Invoke(() => { return (CloudBuildingIndicator.IsBusy); }) ?? false;
        set => CloudBuildingIndicator?.Dispatcher?.Invoke(() => { CloudBuildingIndicator.IsBusy = value; DoEvents(); });
    }

    private SKColor? CloudBackgroundColor = null;
    private SKImage? CloudBackgroundImage = null;
    private SKImage? CloudMaskImage = null;

    private SKImage? ImageToBW(SKImage? image)
    {
        if (image is null) return (null);

        var filter_c = SKColorFilter.CreateTable(
                [.. Enumerable.Range(0, 256).Select(i => (byte)(i > 0 ? 255 : 255))],
                [.. Enumerable.Range(0, 256).Select(i => (byte)(i > 0 ? 255 : 0))],
                [.. Enumerable.Range(0, 256).Select(i => (byte)(i > 0 ? 255 : 0))],
                [.. Enumerable.Range(0, 256).Select(i => (byte)(i > 0 ? 255 : 0))]
            );
        var rect = new SKRectI(0, 0, image?.Width ?? CloudWidth, image?.Height ?? CloudHeight);
        var filter = SKImageFilter.CreateColorFilter(filter_c);
        return(image?.ApplyImageFilter(filter, rect, rect, out SKRectI rect_o, out SKPoint pt_o));
    }

    private SKImage? ImageToWB(SKImage? image)
    {
        if (image is null) return (null);

        var filter_c = SKColorFilter.CreateTable(
                [.. Enumerable.Range(0, 256).Select(i => (byte)(i > 0 ? 255 : 255))],
                [.. Enumerable.Range(0, 256).Select(i => (byte)(i > 0 ? 0 : 255))],
                [.. Enumerable.Range(0, 256).Select(i => (byte)(i > 0 ? 0 : 255))],
                [.. Enumerable.Range(0, 256).Select(i => (byte)(i > 0 ? 0 : 255))]
            );
        var rect = new SKRectI(0, 0, image?.Width ?? CloudWidth, image?.Height ?? CloudHeight);
        var filter = SKImageFilter.CreateColorFilter(filter_c);
        return (image?.ApplyImageFilter(filter, rect, rect, out SKRectI rect_o, out SKPoint pt_o));
    }

    private SKImage? PickImage()
    {
        SKImage? result = null; 
        var dlg = new Microsoft.Win32.OpenFileDialog()
        {
            AddToRecent = false,
            AddExtension = true,
            CheckFileExists = true,
            Filter = "PNG Image|*.png",
            DefaultExt = "png",
        };
        if (dlg.ShowDialog() == true)
        {
            result = SKImage.FromEncodedData(dlg.FileName);
        }
        return (result);
    }

    private void SaveImage()
    {
        if (PngBytes is not null)
        {
            var dlg = new Microsoft.Win32.SaveFileDialog()
            {
                AddToRecent = false,
                AddExtension = true,
                OverwritePrompt = true,
                CheckFileExists = false,
                Filter = "PNG Image|*.png",
                DefaultExt = "png",
                FileName = $"wordcloud-{DateTime.Now:yyyyMMddhhmmss}.png"
            };
            if (dlg.ShowDialog() == true)
            {
                File.WriteAllBytes(dlg.FileName, PngBytes);
            }
        }
    }

    private DispatcherTimer? _DelayMakeTimer_;

    static IEnumerable<WordScore> MakeDemoScore()
    {
        string text = """
        459    cloud
        112    Retrieved
        88    form
        78    species
        74    meteorolog
        66    type
        62    ed.
        54    edit
        53    cumulus
        52    World
        50    into
        50    level
        49    Organization
        49    air
        48    International
        48    atmosphere
        48    troposphere
        45    Atlas
        45    genus
        44    cirrus
        43    convection
        42    vertical
        40    altitude
        40    stratocumulus
        38    high
        38    weather
        37    climate
        37    layer
        36    cumulonimbus
        35    appear
        35    variety
        34    more
        34    water
        33    altocumulus
        33    feature
        33    low
        31    formation
        31    other
        28    precipitation
        28    produce
        27    .mw-parser-output
        27    name
        27    surface
        27    very
        26    base
        25    April
        25    ISBN
        25    also
        25    origin
        24    cirrocumulus
        24    color
        24    cool
        24    stratiformis
        24    stratus
        23    Earth
        23    structure
        22    altostratus
        22    cumuliform
        22    doi
        22    genera
        22    physics
        22    result
        22    see
        22    stratiform
        22    usual
        21    general
        21    p.
        21    seen
        21    temperature
        21    than
        20    polar
        20    space
        20    top
        19    Bibcode
        19    National
        19    cirrostratus
        19    fog
        19    lift
        19    mostly
        19    over
        18    Archived
        18    Latin
        18    Universe
        18    group
        18    sometimes
        18    supplementary
        18    tower
        18    warm
        17    January
        17    change
        17    light
        17    main
        17    nimbostratus
        17    occur
        17    only
        17    stratocumuliform
        17    unstable
        17    wind
        16    associated
        16    cause
        16    global
        16    instability
        16    mid-level
        16    nasa
        16    new
        16    noctilucent
        16    range
        16    science
        16    show
        16    tend
        15    accessory
        15    any
        15    cirriform
        15    droplets
        15    during
        15    extent
        15    multi-level
        15    near
        15    one
        15    rain
        15    reflect
        15    stratosphere
        15    used
        14    J.
        14    October
        14    PDF
        14    airmass
        14    become
        14    common
        14    converge
        14    crystal
        14    cyclone
        14    front
        14    high-level
        14    observes
        14    stable
        14    system
        14    white
        13    all
        13    along
        13    because
        13    classify
        13    each
        13    fibratus
        13    higher
        13    identify
        13    increase
        13    large
        13    mesosphere
        13    sun
        12    castellanus
        12    classification
        12    compose
        12    condenses
        12    congestus
        12    effect
        12    fractus
        12    heavy
        12    include
        12    low-level
        12    moderate
        12    process
        12    radiation
        12    resemble
        12    solar
        12    term
        12    vapor
        12    zone
        11    August
        11    Ci
        11    February
        11    July
        11    M.
        11    March
        11    November
        11    b.
        11    conditions
        11    different
        11    ground
        11    known
        11    lead
        11    most
        11    occasional
        11    often
        11    perlucidus
        11    pressure
        11    research
        11    satellite
        11    saturated
        11    severe
        11    storm
        11    thunderstorm
        11    tornado
        11    visible
        """;

        return (MakeScore(text));
    }

    private static string PreprocessText(string text)
    {
        var result = string.Empty;
        var words = text + "\n";
#pragma warning disable IDE0079 // 请删除不必要的忽略
#pragma warning disable SYSLIB1045 // 转换为“GeneratedRegexAttribute”。
        var results = new List<string>();
        var lines = Regex.Split(words, @"(\n\r|\r\n|\n|\r)", RegexOptions.IgnoreCase).Where(l => !string.IsNullOrEmpty(l.Trim()));
        foreach (var line in lines)
        {
            if (Regex.IsMatch(line, @"^[\s\t]*?(\d{1,6})[\t\s]+(.+?)$", RegexOptions.IgnoreCase) && !Regex.IsMatch(line, @"^[\s\t]*?(\d{1,6})[\t\s]+(\d+)$", RegexOptions.IgnoreCase))
            {
                results.Add(Regex.Replace(line + "\n", @"(\d+)[\t\s]+(.+?)$", "$1\t$2\n", RegexOptions.IgnoreCase).Trim());
            }
            else if (Regex.IsMatch(line, @"^[\s\t]*?(.+?)[\t\s]+(\d{1,6})$", RegexOptions.IgnoreCase) && !Regex.IsMatch(line, @"^[\s\t]*?(\d+)[\t\s]+(\d{1,6})$", RegexOptions.IgnoreCase))
            {
                results.Add(Regex.Replace(line + "\n", @"(.+?)[\t\s]+(\d+)$", "$2\t$1\n", RegexOptions.IgnoreCase).Trim());
            }
        }
        result = string.Join("\n", results);
#pragma warning restore SYSLIB1045 // 转换为“GeneratedRegexAttribute”。
        return (result.Trim());
#pragma warning restore IDE0079 // 请删除不必要的忽略
    }

    private static IEnumerable<WordScore> MakeScore(string? text = null)
    {
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(text.Trim()))
        {
            return (MakeDemoScore());
        }
        else
        {
            var words = PreprocessText(text);
            var scores = words
                    .Split("\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(x => x.Trim().Split("\t")).Where(x => x.Length >= 2)
                    .Select(x => new WordScore(Score: int.Parse(x[0]), Word: x[1]))
                    .Where(x => !string.IsNullOrEmpty(x.Word.Trim()));
            return (scores);
        }
    }

    private CancellationTokenSource _CancelBuilding_ = new(TimeSpan.FromSeconds(60));

    private async void MakeWordsCloud()
    {
        _CancelBuilding_ = new(TimeSpan.FromSeconds(60));
        await Task.Run(() => 
        {
            try
            {
                IsCloudBuilding = true;
                var cloud_width = CloudWidth;
                var cloud_height = CloudHeight;
                var cloud_margin = CloudMargin;
                var cloud_bgstyle = CloudBG;
                var cloud_maskstyle = CloudMask;
                var cloud_orientation = CloudOrientation;
                var cloud_fontfamily = CloudFontFamily;
                var cloud_content = CloudWords;

                if (cloud_bgstyle == CloudBGStyle.Image && CloudBackgroundImage is not null)
                {
                    cloud_width = CloudBackgroundImage.Width;
                    cloud_height = CloudBackgroundImage.Height;
                }
                if (_CancelBuilding_.IsCancellationRequested) return;
                DoEvents();

                MaskOptions? mask = null;
                if (CloudMaskImage is not null)
                {
                    if (cloud_maskstyle == CloudMaskStyle.ImageBW)
                    {
                        cloud_width = CloudMaskImage.Width;
                        cloud_height = CloudMaskImage.Height;
                        mask = MaskOptions.CreateWithBackgroundColor(SKBitmap.FromImage(ImageToBW(CloudMaskImage)), SKColors.White);
                    }
                    else if (cloud_maskstyle == CloudMaskStyle.ImageWB)
                    {
                        cloud_width = CloudMaskImage.Width;
                        cloud_height = CloudMaskImage.Height;
                        mask = MaskOptions.CreateWithBackgroundColor(SKBitmap.FromImage(ImageToWB(CloudMaskImage)), SKColors.White);
                    }
                    if (_CancelBuilding_.IsCancellationRequested) return;
                    DoEvents();
                }
                Sdcb.WordClouds.WordCloud wc = Sdcb.WordClouds.WordCloud.Create(new WordCloudOptions(Math.Max(128, cloud_width), Math.Max(128, cloud_height), MakeScore(cloud_content))
                {
                    Mask = mask,
                    TextOrientation = cloud_orientation,
                    FontManager = new FontManager([SKTypeface.FromFamilyName(cloud_fontfamily)])
                });
                if (_CancelBuilding_.IsCancellationRequested) return;
                DoEvents();

                var wc_img = wc.ToSKBitmap();
                if (cloud_bgstyle != CloudBGStyle.None && cloud_margin == 0)
                {
                    using var bg = new SKBitmap(cloud_width, cloud_height, SKColorType.Rgba8888, SKAlphaType.Premul, SKColorSpace.CreateSrgb());
                    using var bgc = new SKCanvas(bg);
                    if (cloud_bgstyle == CloudBGStyle.White) { bgc?.Clear(SKColors.White); }
                    else if (cloud_bgstyle == CloudBGStyle.Black) { bgc?.Clear(SKColors.Black); }
                    else if (cloud_bgstyle == CloudBGStyle.Color) { bgc?.Clear(CloudBackgroundColor ?? SKColors.Transparent); }
                    else if (cloud_bgstyle == CloudBGStyle.Image) { if (CloudBackgroundImage is not null) bgc?.DrawImage(CloudBackgroundImage, 0, 0); }
                    bgc?.DrawImage(SKImage.FromBitmap(wc_img), 0, 0);
                    bg.CopyTo(wc_img);
                }
                if (_CancelBuilding_.IsCancellationRequested) return;
                DoEvents();

                if (CloudMargin > 0)
                {
                    var margin_w = cloud_width + cloud_margin * 2;
                    var margin_h = cloud_height + cloud_margin * 2;
                    using var margin_img = new SKBitmap(margin_w, margin_h, SKColorType.Rgba8888, SKAlphaType.Premul, SKColorSpace.CreateSrgb());
                    using var margin_c = new SKCanvas(margin_img);
                    if (cloud_bgstyle == CloudBGStyle.None) { margin_c?.Clear(SKColors.Transparent); }
                    else if (cloud_bgstyle == CloudBGStyle.White) { margin_c?.Clear(SKColors.White); }
                    else if (cloud_bgstyle == CloudBGStyle.Black) { margin_c?.Clear(SKColors.Black); }
                    else if (cloud_bgstyle == CloudBGStyle.Color) { margin_c?.Clear(CloudBackgroundColor ?? SKColors.Transparent); }
                    else if (cloud_bgstyle == CloudBGStyle.Image) { if (CloudBackgroundImage is not null) margin_c?.DrawImage(CloudBackgroundImage, new SKRect(0, 0, cloud_width, cloud_height), new SKRect(0, 0, margin_w, margin_h)); }
                    margin_c?.DrawImage(SKImage.FromBitmap(wc_img), cloud_margin, cloud_margin);
                    margin_img?.CopyTo(wc_img);
                }
                if (_CancelBuilding_.IsCancellationRequested) return;
                DoEvents();

                PngBytes = wc_img.Encode(SKEncodedImageFormat.Png, 100).AsSpan().ToArray();
                WordCloudResult = BitmapFrame.Create(new MemoryStream(PngBytes), BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
                DoEvents();
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() => MessageBox.Show(this, ex.StackTrace));
            }
            finally
            {
                IsCloudBuilding = false;
            }

        }, _CancelBuilding_.Token);
    }
    
    private void DelayMakeWordsCloud()
    {
        if (_DelayMakeTimer_ is null)
        {
            _DelayMakeTimer_ ??= new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(2000), IsEnabled = false };
            _DelayMakeTimer_.Tick += (s, e) =>
            {
                _CancelBuilding_?.Cancel();
                _DelayMakeTimer_.Stop();
                MakeWordsCloud();
            };
        }
        if (_DelayMakeTimer_ is not null && WordCloudImage.Source is not null)
        {
            _DelayMakeTimer_.IsEnabled = true;
            _DelayMakeTimer_.Stop();
            _DelayMakeTimer_.Start();
        }
    }
    #endregion

    #region Locale UI
    private void LocaleUI(CultureInfo? culture = null)
    {
        culture ??= CultureInfo.CurrentUICulture;
        Title = $"Title".T(culture) ?? Title;
        this.Locale(culture);
    }
    #endregion

    public MainWindow()
    {
        InitializeComponent();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        #region allow drag and drop text
        WordsTextBox.AllowDrop = true;
        WordsTextBox.DragOver += (s, e) =>
        {
            if (e.Data.GetDataPresent(DataFormats.Text) || e.Data.GetDataPresent(DataFormats.UnicodeText) || e.Data.GetDataPresent(DataFormats.OemText))
            {
                e.Effects = DragDropEffects.Copy;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
            e.Handled = true;
        };
        WordsTextBox.Drop += (s, e) =>
        {
            if (e.Data.GetDataPresent(DataFormats.Text) || e.Data.GetDataPresent(DataFormats.UnicodeText) || e.Data.GetDataPresent(DataFormats.OemText))
            {
                try 
                { 
                    var text = (e.Data.GetData(DataFormats.Text) ?? e.Data.GetData(DataFormats.UnicodeText) ?? e.Data.GetData(DataFormats.OemText) ?? string.Empty) as string;
                    if (!string.IsNullOrEmpty(text))
                    {
                        WordsTextBox.Text = text;
                    }
                    e.Handled = true;
                }
                catch { }
            }
        };
        #endregion

        #region init cloud options to UI
        CloudFontValue.ItemsSource = Fonts.SystemFontFamilies;
        CloudOrientationValue.ItemsSource = Enum.GetValues<TextOrientations>().Cast<TextOrientations>();
        CloudBackgroundValue.ItemsSource = Enum.GetValues<CloudBGStyle>().Cast<CloudBGStyle>();
        CloudMaskImageValue.ItemsSource = Enum.GetValues<CloudMaskStyle>().Cast<CloudMaskStyle>();

        CloudWidth = 1024;
        CloudHeight = 1024;
        CloudMargin = 0;

        CloudFontFamily = "Consolas";
        CloudBG = CloudBGStyle.None;
        CloudMask = CloudMaskStyle.None;
        CloudOrientation = TextOrientations.PreferHorizontal;
        #endregion

        LocaleUI();

        WordsTextBox.Focusable = true;
        WordsTextBox.Focus();

        _DelayMakeTimer_?.Stop();
        //MakeWordsCloud();
    }

    private void BuildWordsCloud_Click(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        _CancelBuilding_?.Cancel();
        _DelayMakeTimer_?.Stop();
        MakeWordsCloud();
    }

    private void SaveWordsCloud_Click(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        _DelayMakeTimer_?.Stop();
        SaveImage();
    }

    private void CloudWidthValue_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!IsLoaded) return;
        _DelayMakeTimer_?.Stop();
        DelayMakeWordsCloud();
    }

    private void CloudHeightValue_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!IsLoaded) return;
        _DelayMakeTimer_?.Stop();
        DelayMakeWordsCloud();
    }

    private void CloudFontValue_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        _DelayMakeTimer_?.Stop();
        DelayMakeWordsCloud();
    }

    private void CloudOrientationValue_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        _DelayMakeTimer_?.Stop();
        DelayMakeWordsCloud();
    }

    private void CloudBackgroundValue_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        _DelayMakeTimer_?.Stop();
        if (e.AddedItems.Count > 0 && e.AddedItems[0] is not null && CloudBackgroundImage is null)
        {
            var new_bg = e.AddedItems.Cast<CloudBGStyle>().FirstOrDefault();
            if (new_bg == CloudBGStyle.Image) { CloudBackImagePick_Click(sender, e); }
        }
        DelayMakeWordsCloud();
    }

    private void CloudBackColorPick_SelectedColorChanged(object sender, RoutedPropertyChangedEventArgs<Color?> e)
    {
        if (IsLoaded && CloudBG == CloudBGStyle.Color) _DelayMakeTimer_?.Stop();
        var c = CloudBackColorPick.SelectedColor;
        CloudBackgroundColor = new SKColor(c?.R ?? 0, c?.G ?? 0, c?.B ?? 0, c?.A ?? 255);
        if (IsLoaded && CloudBG == CloudBGStyle.Color) DelayMakeWordsCloud();
    }

    private void CloudBackImagePick_Click(object sender, RoutedEventArgs e)
    {
        CloudBackgroundImage = PickImage();
        CloudWidth = CloudBackgroundImage?.Width ?? CloudWidth;
        CloudHeight = CloudBackgroundImage?.Height ?? CloudHeight;
    }

    private void CloudMaskImageValue_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        _DelayMakeTimer_?.Stop();
        if (e.AddedItems.Count > 0 && e.AddedItems[0] is not null && CloudMaskImage is null)
        {
            var new_mask = e.AddedItems.Cast<CloudMaskStyle>().FirstOrDefault();
            if (new_mask == CloudMaskStyle.ImageBW || new_mask == CloudMaskStyle.ImageWB) { CloudMaskImagePick_Click(sender, e); }
        }
        DelayMakeWordsCloud();
    }

    private void CloudMaskImagePick_Click(object sender, RoutedEventArgs e)
    {
        CloudMaskImage = PickImage();
        CloudWidth = CloudMaskImage?.Width ?? CloudWidth;
        CloudHeight = CloudMaskImage?.Height ?? CloudHeight;
    }

    private void CloudMarginValue_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!IsLoaded) return;
        _DelayMakeTimer_?.Stop();
        DelayMakeWordsCloud();
    }
}