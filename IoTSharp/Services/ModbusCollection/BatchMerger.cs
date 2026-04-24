using IoTSharp.Data;
using System.Collections.Generic;
using System.Linq;

namespace IoTSharp.Services.ModbusCollection;

/// <summary>
/// 批量合并器 - 将多个点位合并为批量读取请求
/// </summary>
public class BatchMerger
{
    /// <summary>
    /// 合并点位为批量读取请求
    /// 规则：同从站 + 连续地址 + 相同功能码 = 合并
    /// </summary>
    public List<BatchRequest> Merge(IEnumerable<CollectionPoint> points)
    {
        var batches = new List<BatchRequest>();

        // 1. 按 (SlaveId, FunctionCode) 分组
        var groups = points
            .Where(p => p.Enabled)
            .GroupBy(p => new
            {
                p.Device.SlaveId,
                p.FunctionCode,
                p.Device
            })
            .ToList();

        foreach (var group in groups)
        {
            // 2. 按地址排序
            var sortedPoints = group.OrderBy(p => p.Address).ToList();

            // 3. 查找连续地址序列并生成批量请求
            var continuousRanges = FindContinuousRanges(sortedPoints);

            foreach (var range in continuousRanges)
            {
                var batch = new BatchRequest
                {
                    SlaveId = group.Key.SlaveId,
                    FunctionCode = group.Key.FunctionCode,
                    StartAddress = range.StartAddress,
                    Quantity = (ushort)range.Count,
                    Points = range.Points,
                    Device = group.Key.Device
                };
                batches.Add(batch);
            }
        }

        return batches;
    }

    /// <summary>
    /// 查找连续地址范围
    /// </summary>
    private List<ContinuousRange> FindContinuousRanges(List<CollectionPoint> sortedPoints)
    {
        var ranges = new List<ContinuousRange>();

        if (sortedPoints.Count == 0)
            return ranges;

        var currentRange = new ContinuousRange
        {
            StartAddress = sortedPoints[0].Address,
            Count = 1,
            Points = new List<CollectionPoint> { sortedPoints[0] }
        };

        for (int i = 1; i < sortedPoints.Count; i++)
        {
            var point = sortedPoints[i];
            var expectedAddress = (ushort)(currentRange.StartAddress + currentRange.Count);

            // 检查是否是连续地址
            // 注意：同功能码和连续寄存器才是连续的
            if (point.Address == expectedAddress)
            {
                currentRange.Count++;
                currentRange.Points.Add(point);
            }
            else
            {
                // 地址不连续，保存当前范围，开始新范围
                ranges.Add(currentRange);
                currentRange = new ContinuousRange
                {
                    StartAddress = point.Address,
                    Count = 1,
                    Points = new List<CollectionPoint> { point }
                };
            }
        }

        // 添加最后一个范围
        ranges.Add(currentRange);

        return ranges;
    }

    /// <summary>
    /// 优化批量请求 - 将相邻的小批量请求合并
    /// </summary>
    public List<BatchRequest> Optimize(List<BatchRequest> batches, ushort maxQuantity = 125)
    {
        if (batches.Count <= 1)
            return batches;

        var optimized = new List<BatchRequest>();
        var sorted = batches.OrderBy(b => b.SlaveId).ThenBy(b => b.StartAddress).ToList();

        var current = sorted[0];

        for (int i = 1; i < sorted.Count; i++)
        {
            var next = sorted[i];

            // 检查是否可以合并
            if (current.SlaveId == next.SlaveId &&
                current.FunctionCode == next.FunctionCode &&
                current.StartAddress + current.Quantity == next.StartAddress &&
                current.Quantity + next.Quantity <= maxQuantity)
            {
                // 合并
                current.Quantity += next.Quantity;
                current.Points.AddRange(next.Points);
            }
            else
            {
                optimized.Add(current);
                current = next;
            }
        }

        optimized.Add(current);
        return optimized;
    }
}

/// <summary>
/// 批量请求
/// </summary>
public class BatchRequest
{
    /// <summary>
    /// 从站地址
    /// </summary>
    public byte SlaveId { get; set; }

    /// <summary>
    /// 功能码
    /// </summary>
    public byte FunctionCode { get; set; }

    /// <summary>
    /// 起始地址
    /// </summary>
    public ushort StartAddress { get; set; }

    /// <summary>
    /// 数量
    /// </summary>
    public ushort Quantity { get; set; }

    /// <summary>
    /// 包含的点位
    /// </summary>
    public List<CollectionPoint> Points { get; set; } = new();

    /// <summary>
    /// 所属从站
    /// </summary>
    public CollectionDevice Device { get; set; }
}

/// <summary>
/// 连续地址范围
/// </summary>
internal class ContinuousRange
{
    public ushort StartAddress { get; set; }
    public int Count { get; set; }
    public List<CollectionPoint> Points { get; set; } = new();
}