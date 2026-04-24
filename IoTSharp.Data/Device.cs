using IoTSharp.Contracts;
using System;

namespace IoTSharp.Data
{
    /// <summary>
    /// 设备
    /// </summary>
    public class Device
    {
        /// <summary>
        /// 设备ID
        /// </summary>
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>
        /// 设备名称
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 设备类型
        /// </summary>
        public DeviceType DeviceType { get; set; }


        /// <summary>
        /// 超时时间 秒数
        /// </summary>
        public int Timeout { get; set; }

        /// <summary>
        /// 所有者
        /// </summary>
        public Gateway Owner { get; set; }

        /// <summary>
        /// 租户
        /// </summary>
        public Tenant Tenant { get; set; }

        /// <summary>
        /// 客户
        /// </summary>
        public Customer Customer { get; set; }


        public Guid? DeviceModelId { get; set; }

        public DeviceIdentity DeviceIdentity { get; set; }

        public bool Deleted { get; set; }

        /// <summary>
        /// HVAC 设备类型（可选）
        /// </summary>
        public HVACDeviceType? HvacDeviceType { get; set; }

        /// <summary>
        /// 绑定的设备类型模板ID
        /// </summary>
        public Guid? DeviceTypeProfileId { get; set; }

        /// <summary>
        /// 导航属性：设备类型模板
        /// </summary>
        public DeviceTypeProfile DeviceTypeProfile { get; set; }

    }
}