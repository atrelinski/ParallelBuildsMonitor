using System;
using System.Globalization;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Timers;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using EnvDTE;
using EnvDTE80;

namespace ParallelBuildsMonitor
{
  public class GraphControl : ContentControl
  {
    public bool scrollLast = true;
    public string intFormat = "D3";
    public bool isBuilding = false;

    // pun any colors here, set final theme dependent colors in OnForegroundChanged
    Brush blueSolidBrush = new SolidColorBrush(Colors.DarkBlue);
    Pen blackPen = new Pen(new SolidColorBrush(Colors.Black), 1.0);
    Brush blackBrush = new SolidColorBrush(Colors.Black);
    Brush greenSolidBrush = new SolidColorBrush(Colors.DarkGreen);
    Brush redSolidBrush = new SolidColorBrush(Colors.DarkRed);
    Brush whiteBrush = new SolidColorBrush(Colors.White);
    Pen grid = new Pen(new SolidColorBrush(Colors.LightGray), 1.0);
    Pen cpuPen = new Pen(new SolidColorBrush(Colors.Black), 1.0);
    Pen hddPen = new Pen(new SolidColorBrush(Colors.Black), 1.0);
    public Timer timer = new Timer();

    public GraphControl()
    {
      Instance = this;
      OnForegroundChanged();
    }

    void OnForegroundChanged()
    {
      bool isDark = false;
      SolidColorBrush foreground = Foreground as SolidColorBrush;
      if (foreground != null)
      {
        isDark = foreground.Color.R > 0x80;
      }

      blackBrush = new SolidColorBrush(isDark ? Colors.White : Colors.Black);
      Background = Brushes.Transparent;
      greenSolidBrush = new SolidColorBrush(isDark ? Colors.LightGreen : Colors.DarkGreen);
      redSolidBrush = new SolidColorBrush(isDark ? Color.FromRgb(249, 33, 33) : Colors.DarkRed);
      blueSolidBrush = new SolidColorBrush(isDark ? Color.FromRgb(81, 156, 245) : Colors.DarkBlue);
      blackPen = new Pen(new SolidColorBrush(isDark ? Colors.White : Colors.Black), 1.0);
      grid = new Pen(new SolidColorBrush(isDark ? Color.FromRgb(66, 66, 66) : Colors.LightGray), 1.0);
      cpuPen = new Pen(new SolidColorBrush(isDark ? Colors.LightSeaGreen : Colors.DarkTurquoise), 1.0);
      hddPen = new Pen(new SolidColorBrush(isDark ? Colors.LightSkyBlue : Colors.DarkViolet), 1.0);
    }

    public static GraphControl Instance
    {
      get;
      private set;
    }

    void drawGraph(string title, System.Windows.Media.DrawingContext drawingContext, List<Tuple<long, float>> data, Pen pen, ref int i, Size RenderSize, double rowHeight, double maxStringLength, long maxTick, Typeface fontFace, bool showAverage)
    {
      // Status separator
      drawingContext.DrawLine(grid, new Point(0, i * rowHeight), new Point(RenderSize.Width, i * rowHeight));

      // Draw graph
      FormattedText itext = new FormattedText(title, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, fontFace, FontSize, blackBrush);
      drawingContext.DrawText(itext, new Point(1, i * rowHeight));

      if (data.Count > 0)
      {
        double pixelsRange = RenderSize.Width - maxStringLength;

        ParallelBuildsMonitorWindowCommand host = ParallelBuildsMonitorWindowCommand.Instance;

        double sumValues = 0;
        long sumTicks = 0;
        long previousTick = host.buildTime.Ticks;
        float previousValue = 0.0f;
        for (int nbr = 0; nbr < data.Count; nbr++)
        {
          long spanL = previousTick - host.buildTime.Ticks;
          long spanR = data[nbr].Item1 - host.buildTime.Ticks;
          double shiftL = (int)(spanL * (long)(RenderSize.Width - maxStringLength) / maxTick);
          double shiftR = (int)(spanR * (long)(RenderSize.Width - maxStringLength) / maxTick);
          drawingContext.DrawLine(pen, new Point(maxStringLength + shiftL, (i + 1) * rowHeight - Math.Min(previousValue, 100.0) * (rowHeight - 2) / 100 - 1),
                                       new Point(maxStringLength + shiftR, (i + 1) * rowHeight - Math.Min(data[nbr].Item2, 100.0) * (rowHeight - 2) / 100 - 1));

          if (showAverage)
          {
            sumValues += (previousValue + data[nbr].Item2) * (data[nbr].Item1 - previousTick);
            sumTicks += data[nbr].Item1 - previousTick;
          }

          previousTick = data[nbr].Item1;
          previousValue = data[nbr].Item2;
        }

        if (showAverage)
        {
          FormattedText avg = new FormattedText("Avg. " + ((long)(sumValues / 2 / sumTicks)).ToString() + "%", CultureInfo.CurrentCulture, FlowDirection.LeftToRight, fontFace, FontSize, blackBrush);
          double m = avg.Width;
          drawingContext.DrawText(avg, new Point(RenderSize.Width - m, i * rowHeight));
        }
      }
      i++;
    }


    protected override void OnRender(System.Windows.Media.DrawingContext drawingContext)
    {
      try
      {
        if (RenderSize.Width < 10.0 || RenderSize.Height < 10.0)
        {
          return;
        }

        if (ParallelBuildsMonitorWindowCommand.Instance == null)
          return;

        DTE2 dte = (DTE2)ParallelBuildsMonitorWindowCommand.Instance.ServiceProvider.GetService(typeof(DTE));

        if (dte == null)
          return;

        ParallelBuildsMonitorWindowCommand host = ParallelBuildsMonitorWindowCommand.Instance;

        if (host.allProjectsCount == 0)
          return;

        Typeface fontFace = new Typeface(FontFamily.Source);

        FormattedText dummyText = new FormattedText("A0", CultureInfo.CurrentCulture, FlowDirection.LeftToRight, fontFace, FontSize, blackBrush);

        double rowHeight = dummyText.Height + 1;
        int linesCount = host.currentBuilds.Count + host.finishedBuilds.Count + 1 + 1 + 1; // 1 for status, 1 for CPU, 1 for HDD
        double totalHeight = rowHeight * linesCount;

        Height = totalHeight;

        double maxStringLength = 0.0;
        long tickStep = 100000000;
        long maxTick = tickStep;
        long nowTick = DateTime.Now.Ticks;
        long t = nowTick - host.buildTime.Ticks;
        if (host.currentBuilds.Count > 0)
        {
          if (t > maxTick)
          {
            maxTick = t;
          }
        }
        int i;
        bool atLeastOneError = false;
        for (i = 0; i < host.finishedBuilds.Count; i++)
        {
          FormattedText iname = new FormattedText(host.finishedBuilds[i].name, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, fontFace, FontSize, blackBrush);
          double l = iname.Width;
          t = host.finishedBuilds[i].end;
          atLeastOneError = atLeastOneError || !host.finishedBuilds[i].success;
          if (t > maxTick)
          {
            maxTick = t;
          }
          if (l > maxStringLength)
          {
            maxStringLength = l;
          }
        }
        foreach (KeyValuePair<string, DateTime> item in host.currentBuilds)
        {
          FormattedText iname = new FormattedText(item.Key, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, fontFace, FontSize, blackBrush);
          double l = iname.Width;
          if (l > maxStringLength)
          {
            maxStringLength = l;
          }
        }
        if (isBuilding)
        {
          maxTick = (maxTick / tickStep + 1) * tickStep;
        }
        FormattedText iint = new FormattedText(i.ToString(intFormat) + " ", CultureInfo.CurrentCulture, FlowDirection.LeftToRight, fontFace, FontSize, blackBrush);
        maxStringLength += 5 + iint.Width;

        Brush greenGradientBrush = new LinearGradientBrush(Colors.MediumSeaGreen, Colors.DarkGreen, new Point(0, 0), new Point(0, 1));
        Brush redGradientBrush = new LinearGradientBrush(Colors.IndianRed, Colors.DarkRed, new Point(0, 0), new Point(0, 1));
        for (i = 0; i < host.finishedBuilds.Count; i++)
        {
          Brush solidBrush = host.finishedBuilds[i].success ? greenSolidBrush : redSolidBrush;
          Brush gradientBrush = host.finishedBuilds[i].success ? greenGradientBrush : redGradientBrush;
          DateTime span = new DateTime(host.finishedBuilds[i].end - host.finishedBuilds[i].begin);
          string time = ParallelBuildsMonitorWindowCommand.SecondsToString(span.Ticks);
          FormattedText itext = new FormattedText((i + 1).ToString(intFormat) + " " + host.finishedBuilds[i].name, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, fontFace, FontSize, solidBrush);
          drawingContext.DrawText(itext, new Point(1, i * rowHeight));
          Rect r = new Rect();
          r.X = maxStringLength + (int)((host.finishedBuilds[i].begin) * (long)(RenderSize.Width - maxStringLength) / maxTick);
          r.Width = maxStringLength + (int)((host.finishedBuilds[i].end) * (long)(RenderSize.Width - maxStringLength) / maxTick) - r.X;
          if (r.Width == 0)
          {
            r.Width = 1;
          }
          r.Y = i * rowHeight + 1;
          r.Height = rowHeight - 1;
          drawingContext.DrawRectangle(gradientBrush, null, r);
          FormattedText itime = new FormattedText(time, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, fontFace, FontSize, whiteBrush);
          double timeLen = itime.Width;
          if (r.Width > timeLen)
          {
            drawingContext.DrawText(itime, new Point(r.Right - timeLen, i * rowHeight));
          }
          drawingContext.DrawLine(grid, new Point(0, i * rowHeight), new Point(RenderSize.Width, i * rowHeight));
        }

        Brush blueGradientBrush = new LinearGradientBrush(Colors.LightBlue, Colors.DarkBlue, new Point(0, 0), new Point(0, 1));
        foreach (KeyValuePair<string, DateTime> item in host.currentBuilds)
        {
          FormattedText itext = new FormattedText((i + 1).ToString(intFormat) + " " + item.Key, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, fontFace, FontSize, blueSolidBrush);
          drawingContext.DrawText(itext, new Point(1, i * rowHeight));
          Rect r = new Rect();
          r.X = maxStringLength + (int)((item.Value.Ticks - host.buildTime.Ticks) * (long)(RenderSize.Width - maxStringLength) / maxTick);
          r.Width = maxStringLength + (int)((nowTick - host.buildTime.Ticks) * (long)(RenderSize.Width - maxStringLength) / maxTick) - r.X;
          if (r.Width == 0)
          {
            r.Width = 1;
          }
          r.Y = i * rowHeight + 1;
          r.Height = rowHeight - 1;
          drawingContext.DrawRectangle(blueGradientBrush, null, r);
          drawingContext.DrawLine(grid, new Point(0, i * rowHeight), new Point(RenderSize.Width, i * rowHeight));
          i++;
        }

        drawGraph("CPU usage", drawingContext, host.cpuUsage, cpuPen, ref i, RenderSize, rowHeight, maxStringLength, maxTick, fontFace, true /*showAverage*/);
        drawGraph("HDD usage", drawingContext, host.hddUsage, hddPen, ref i, RenderSize, rowHeight, maxStringLength, maxTick, fontFace, false /*showAverage - Probably there is no max value for HDD that is why we can't cound average*/);

        if (host.currentBuilds.Count > 0 || host.finishedBuilds.Count > 0)
        {
          string line = "";
          if (isBuilding)
          {
            line = "Building...";
          }
          else
          {
            line = "Done";
          }
          if (host.maxParallelBuilds > 0)
          {
            line += " (" + host.PercentageProcessorUse().ToString() + "% of " + host.maxParallelBuilds.ToString() + " CPUs)";
          }
          FormattedText itext = new FormattedText(line, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, fontFace, FontSize, blackBrush);
          drawingContext.DrawText(itext, new Point(1, i * rowHeight));
        }
        drawingContext.DrawLine(grid, new Point(maxStringLength, 0), new Point(maxStringLength, i * rowHeight));
        drawingContext.DrawLine(new Pen(atLeastOneError ? redSolidBrush : greenSolidBrush, 1), new Point(0, i * rowHeight), new Point(RenderSize.Width, i * rowHeight));

        DateTime dt = new DateTime(maxTick);
        string s = ParallelBuildsMonitorWindowCommand.SecondsToString(dt.Ticks);
        FormattedText maxTime = new FormattedText(s, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, fontFace, FontSize, blackBrush);
        double m = maxTime.Width;
        drawingContext.DrawText(maxTime, new Point(RenderSize.Width - m, i * rowHeight));
      }
      catch (Exception)
      {
      }
    }

    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
      if (e.Property.Name == "Foreground")
      {
        OnForegroundChanged();
      }
      base.OnPropertyChanged(e);
    }

  }
}
