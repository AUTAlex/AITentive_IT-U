using CsvHelper.TypeConversion;
using CsvHelper;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System;

public class BufferedCsvWriter<T>
{
    private readonly List<T> _buffer;
    private readonly int _flushThreshold;
    private readonly string _filePath;
    private readonly bool _overwrite;

    public BufferedCsvWriter(string filePath, bool overwrite = false, int flushThreshold = 100)
    {
        _filePath = filePath;
        _overwrite = overwrite;
        _flushThreshold = flushThreshold;
        _buffer = new List<T>(flushThreshold);
    }

    public void Add(T item)
    {
        _buffer.Add(item);

        if (_buffer.Count >= _flushThreshold)
        {
            Flush();
        }
    }

    public void Flush()
    {
        if (_buffer.Count == 0) return;

        Util.SaveDataToCSV(_filePath, _buffer, _overwrite);

        _buffer.Clear();
    }

    public void Dispose()
    {
        Flush();
    }
}