using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace WordCloud
{
#pragma warning disable IDE0079
#pragma warning disable IDE0038
#pragma warning disable IDE0039
#pragma warning disable IDE0044
#pragma warning disable IDE0060
#pragma warning disable IDE0083
#pragma warning disable SYSLIB1045
#pragma warning disable CS0168
#pragma warning disable CS8601
#pragma warning disable CS8602
#pragma warning disable CS8603
#pragma warning disable CS8625

    public static class UILocale
    {
        #region Locale Resource Helper
        private static CultureInfo resourceCulture = Resources.UI.Culture ?? CultureInfo.CurrentUICulture;
        private static readonly System.Resources.ResourceManager resourceMan = Resources.UI.ResourceManager;
        private static System.Resources.ResourceSet resourceSet = resourceMan.GetResourceSet(resourceCulture, true, true);
        private static Dictionary<FrameworkElement, bool> _be_locale_ = null;

        public static string _(this string text)
        {
            return (GetString(text));
        }

        public static string _(this string text, CultureInfo culture)
        {
            return (GetString(text, culture));
        }

        public static string T(this string text)
        {
            return (GetString(text));
        }

        public static string T(this string text, CultureInfo culture)
        {
            return (GetString(text, culture));
        }

        public static string GetString(this string text)
        {
            var t = resourceSet.GetString(text);
            //return (string.IsNullOrEmpty(t) ? null : t.Replace("\\n", Environment.NewLine));
            return (string.IsNullOrEmpty(t) ? null : Regex.Replace(t, @"(\\n\\r|\\r\\n|\n\r|\r\n|\\n|\\r|\n|\r)", Environment.NewLine));
        }

        public static string GetString(this string text, CultureInfo culture)
        {
            ChangeLocale(culture);
            return (GetString(text));
        }

        public static bool Contains(this string text)
        {
            return (resourceSet.GetString(text) != null);
        }

        public static void Locale(this FrameworkElement element, IEnumerable<string> ignore_uids = null, IEnumerable<FrameworkElement> ignore_elements = null)
        {
            try
            {
                _be_locale_ ??= [];
                if (_be_locale_.ContainsKey(element)) return;

                var element_name = element.Name ?? string.Empty;
                var elemet_uid = element.Uid ?? string.Empty;

                bool trans_uid = !(ignore_uids is not null) || !ignore_uids.Where(uid => uid.Equals(elemet_uid) || uid.Equals(element_name)).Any();
                bool trans_element = !(ignore_elements is not null) || !ignore_elements.Where(e => e == element).Any();

                if (!string.IsNullOrEmpty(elemet_uid))
                {
#if DEBUG
                    Debug.WriteLine($"==> UID: {element.Uid}");
#endif

                    if (trans_uid && trans_element)
                    {
                        if (element is ButtonBase)
                        {
                            var ui = element as ButtonBase;
                            if (ui?.Content is string && !string.IsNullOrEmpty(ui.Content as string))
                            {
                                var text = $"{ui.Uid}.Content".T();
                                if (!string.IsNullOrEmpty(text)) ui.Content = text;
                            }
                        }
                        else if (element is TextBlock)
                        {
                            var ui = element as TextBlock;
                            if (!string.IsNullOrEmpty(ui.Text))
                            {
                                var text = $"{ui.Uid}.Text".T();
                                if (!string.IsNullOrEmpty(text)) ui.Text = text;
                            }
                        }
                        else if (element is MenuItem)
                        {
                            var ui = element as MenuItem;
                            if (ui?.Header is string && !string.IsNullOrEmpty(ui.Header as string))
                            {
                                var text = $"{ui.Uid}.Header".T();
                                if (!string.IsNullOrEmpty(text)) ui.Header = text;
                            }
                            if (ui?.Items.Count > 1)
                                foreach (var i in ui.Items) if (i is FrameworkElement) ((FrameworkElement)i).Locale();
                        }
                        else if (element is MenuBase)
                        {
                            var ui = element as MenuBase;
                            foreach (var i in ui.Items) if (i is FrameworkElement) ((FrameworkElement)i).Locale();
                        }
                        else if (element is ItemsControl)
                        {
                            var ui = element as ItemsControl;
                            foreach (var i in ui.Items) if (i is FrameworkElement) ((FrameworkElement)i).Locale();
                        }
                        //else if (element is ColorPicker)
                        //{
                        //    var ui = element as ColorPicker;
                        //    if (!string.IsNullOrEmpty(ui.AdvancedTabHeader))
                        //    {
                        //        var text = $"{ui.Uid}.AdvancedTabHeader".T();
                        //        if (!string.IsNullOrEmpty(text)) ui.AdvancedTabHeader = text;
                        //    }
                        //    if (!string.IsNullOrEmpty(ui.AdvancedTabHeader))
                        //    {
                        //        var text = $"{ui.Uid}.StandardTabHeader".T();
                        //        if (!string.IsNullOrEmpty(text)) ui.StandardTabHeader = text;
                        //    }
                        //    if (!string.IsNullOrEmpty(ui.AdvancedTabHeader))
                        //    {
                        //        var text = $"{ui.Uid}.AvailableColorsHeader".T();
                        //        if (!string.IsNullOrEmpty(text)) ui.AvailableColorsHeader = text;
                        //    }
                        //    if (!string.IsNullOrEmpty(ui.AdvancedTabHeader))
                        //    {
                        //        var text = $"{ui.Uid}.StandardColorsHeader".T();
                        //        if (!string.IsNullOrEmpty(text)) ui.StandardColorsHeader = text;
                        //    }
                        //    if (!string.IsNullOrEmpty(ui.AdvancedTabHeader))
                        //    {
                        //        var text = $"{ui.Uid}.RecentColorsHeader".T();
                        //        if (!string.IsNullOrEmpty(text)) ui.RecentColorsHeader = text;
                        //    }
                        //}
                    }
                }

                var child_count = VisualTreeHelper.GetChildrenCount(element);
                if (child_count > 0)
                {
                    for (int i = 0; i < child_count; i++)
                    {
                        var child = VisualTreeHelper.GetChild(element, i);
                        if (child is FrameworkElement) ((FrameworkElement)child).Locale();
                    }
                }
                else
                {
                    var childs = LogicalTreeHelper.GetChildren(element);
                    foreach (var child in childs)
                    {
                        if (child is FrameworkElement) ((FrameworkElement)child).Locale();
                    }
                }

                if (element is not null)
                {
                    var ui = element as FrameworkElement;
                    if (trans_uid && trans_element && !string.IsNullOrEmpty(ui.Uid))
                    {
                        var tip = $"{ui.Uid}.ToolTip".T();
                        if (!string.IsNullOrEmpty(tip))
                        {
                            if (ui.ToolTip is string) ui.ToolTip = tip;
                            else if (ui.ToolTip is ToolTip && (ui.ToolTip as ToolTip).Content is string) (ui.ToolTip as ToolTip).Content = tip;
                        }
                    }
                    _be_locale_.TryAdd(element, true);
                    if (ui.ContextMenu is not null) Locale(ui.ContextMenu);
                }
            }
            catch { }
            //catch (Exception ex) { $"Locale : {element.Uid ?? element.ToString()} : {ex.Message}".ShowMessage(); }
        }

        public static void Locale(this FrameworkElement element, CultureInfo culture, IEnumerable<string> ignore_uids = null, IEnumerable<FrameworkElement> ignore_elements = null)
        {
            try
            {
                ChangeLocale(culture);
                Locale(element, ignore_uids: ignore_uids, ignore_elements: ignore_elements);
            }
            catch { }
            //catch (Exception ex) { $"Locale : {ex.Message}".ShowMessage(); }
        }

        public static void Locale(this IEnumerable<FrameworkElement> elements)
        {
            foreach (var element in elements)
            {
                Locale(element);
            }
        }

        public static void Locale(this IEnumerable<FrameworkElement> elements, CultureInfo culture)
        {
            try
            {
                ChangeLocale(culture);
                Locale(elements);
            }
            catch { }
            //catch (Exception ex) { $"Locale : {ex.Message}".ShowMessage(); }
        }

        public static void ChangeLocale(this CultureInfo culture)
        {
            culture ??= CultureInfo.CurrentUICulture;
            if (culture is not null && resourceCulture != culture)
            {
                resourceSet = resourceMan.GetResourceSet(culture, true, true);
                resourceCulture = culture;
                if (_be_locale_ == null) _be_locale_ = [];
                else _be_locale_.Clear();
            }
        }
        #endregion
    }
}
