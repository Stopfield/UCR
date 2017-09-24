﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Dynamic;
using System.Xml.Serialization;
using Providers;
using UCR.Core.Models.Binding;

namespace UCR.Core.Models.Device
{
    public enum DeviceType
    {
        [Description("Joystick")]
        Joystick,
        [Description("Keyboard")]
        Keyboard,
        [Description("Mouse")]
        Mouse,
        [Description("Generic device")]
        Generic
    }

    public enum DeviceIoType
    {
        Input,
        Output
    }

    public class Device
    {
        // Persistance
        public string Title { get; set; }
        public string ProviderName { get; set; }
        public string DeviceHandle { get; set; }

        // Runtime
        [XmlIgnore]
        public Guid Guid { get; }
        [XmlIgnore]
        private Profile.Profile ParentProfile { get; set; }
        [XmlIgnore]
        private List<DeviceBindingNode> DeviceBindingMenu { get; set; }

        private bool IsAcquired { get; set; }

        // Subscriptions
        private Dictionary<string, List<DeviceBinding>> Subscriptions;

        public Device()
        {
            Guid = Guid.NewGuid();
            IsAcquired = false;
            ClearSubscribers();
        }

        public Device(Guid guid = new Guid())
        {
            Guid = (guid == Guid.Empty) ? Guid.NewGuid() : guid;
            IsAcquired = false;
            ClearSubscribers();
        }

        public Device(Device device) : this(device.Guid)
        {
            Title = device.Title;
            DeviceHandle = device.DeviceHandle;
            ProviderName = device.ProviderName;
            DeviceBindingMenu = device.DeviceBindingMenu;
            Guid = device.Guid;
        }

        public Device(DeviceReport device, ProviderReport providerReport, DeviceIoType type) : this()
        {
            Title = device.DeviceName;
            ProviderName = providerReport.ProviderDescriptor.ProviderName;
            DeviceHandle = device.DeviceDescriptor.DeviceHandle;
            DeviceBindingMenu = GetDeviceBindingMenu(device.Nodes, type);
        }

        private static List<DeviceBindingNode> GetDeviceBindingMenu(List<DeviceReportNode> deviceNodes, DeviceIoType type)
        {
            var result = new List<DeviceBindingNode>();
            if (deviceNodes == null) return result;

            foreach (var deviceNode in deviceNodes)
            {
                var groupNode = new DeviceBindingNode()
                {
                    Title = deviceNode.Title,
                    IsBinding = false,
                    ChildrenNodes = GetDeviceBindingMenu(deviceNode.Nodes, type),
                    
                };
                
                foreach (var bindingInfo in deviceNode.Bindings)
                {
                    var bindingNode = new DeviceBindingNode()
                    {
                        Title = bindingInfo.Title,
                        IsBinding = true,
                        DeviceBinding = new DeviceBinding(null,null,type)
                        {
                            IsBound = false,
                            KeyType = (int)bindingInfo.BindingDescriptor.Type,
                            KeyValue = bindingInfo.BindingDescriptor.Index,
                            KeySubValue = bindingInfo.BindingDescriptor.SubIndex,
                            DeviceBindingCategory = DeviceBinding.MapCategory(bindingInfo.Category)
                            // TODO Extract to class instead of using DeviceBinding?
                        }

                    };
                    if (groupNode.ChildrenNodes == null)
                    {
                        groupNode.ChildrenNodes = new List<DeviceBindingNode>();
                    }

                    groupNode.ChildrenNodes.Add(bindingNode);
                }
                result.Add(groupNode);
            }
            return result.Count != 0 ? result : null;
        }

        public void WriteOutput(Context context, DeviceBinding deviceBinding, long value)
        {
            if (DeviceHandle == null || ProviderName == null) return;
            context.IOController.SetOutputstate(GetOutputSubscriptionRequest(), GetBindingDescriptor(deviceBinding), (int)value);
        }

        public bool AddDeviceBinding(DeviceBinding deviceBinding)
        {
            List<DeviceBinding> currentSub = null;
            if (Subscriptions.ContainsKey(deviceBinding.Plugin.Title))
            {
                currentSub = Subscriptions[deviceBinding.Plugin.Title];
            }

            if (currentSub == null || currentSub.Count == 0)
            {
                Subscriptions[deviceBinding.Plugin.Title] = new List<DeviceBinding> {deviceBinding};
                return true;
            }
            
            // Override bindings if Profile parent does not match. Root is loaded first and active profile last
            if (!string.Equals(currentSub[0].Plugin.ParentProfile.Title, deviceBinding.Plugin.ParentProfile.Title))
            {
                Subscriptions[deviceBinding.Plugin.Title] = new List<DeviceBinding> {deviceBinding};
            }
            else
            {
                var existingBinding = currentSub.Find(b => b.Guid == deviceBinding.Guid);
                if (existingBinding != null)
                {
                    // Remove existing binding if it exists
                    Subscriptions[deviceBinding.Plugin.Title].Remove(existingBinding);
                }
                Subscriptions[deviceBinding.Plugin.Title].Add(deviceBinding);
            }

            return true;
        }

        private void ClearSubscribers()
        {
            Subscriptions = new Dictionary<string, List<DeviceBinding>>();
        }

        public bool SubscribeDeviceBindings(Context context)
        {
            var success = true;
            foreach (var deviceBindingList in Subscriptions)
            {
                foreach (var deviceBinding in deviceBindingList.Value)
                {
                    success &= SubscribeDeviceBindingInput(context, deviceBinding);
                }
            }
            return success;
        }

        public bool UnsubscribeDeviceBindings(Context context)
        {
            var success = true;
            foreach (var deviceBindingList in Subscriptions)
            {
                foreach (var deviceBinding in deviceBindingList.Value)
                {
                    success &= UnsubscribeDeviceBindingInput(context, deviceBinding);
                }
            }
            return success;
        }

        /// <summary>
        /// Subscribe a devicebining with the backend. Unsubscribe devicebinding if it is not bound
        /// </summary>
        /// <param name="context"></param>
        /// <param name="deviceBinding"></param>
        /// <returns>If the subscription succeeded</returns>
        public bool SubscribeDeviceBindingInput(Context context, DeviceBinding deviceBinding)
        {
            return deviceBinding.IsBound 
                ? context.IOController.SubscribeInput(GetInputSubscriptionRequest(deviceBinding))
                : UnsubscribeDeviceBindingInput(context, deviceBinding);
        }

        public bool UnsubscribeDeviceBindingInput(Context context, DeviceBinding deviceBinding)
        {
            return context.IOController.UnsubscribeInput(GetInputSubscriptionRequest(deviceBinding));
        }

        public string GetBindingName(DeviceBinding deviceBinding)
        {
            if (!deviceBinding.IsBound) return "Not bound";
            return GetBindingName(deviceBinding, DeviceBindingMenu) ?? "Unknown input";
        }

        private static string GetBindingName(DeviceBinding deviceBinding, List<DeviceBindingNode> deviceBindingNodes)
        {
            if (deviceBindingNodes == null) return null;
            foreach (var deviceBindingNode in deviceBindingNodes)
            {
                if (deviceBindingNode.IsBinding && deviceBindingNode.DeviceBinding.KeyType == deviceBinding.KeyType && deviceBindingNode.DeviceBinding.KeyValue == deviceBinding.KeyValue)
                {
                    return deviceBindingNode.Title;
                }
                var name = GetBindingName(deviceBinding, deviceBindingNode.ChildrenNodes);
                if (name != null)
                {
                    return deviceBindingNode.Title + ", " + name;
                }
            }
            return null;
        }

        public bool SubscribeOutput(Context context)
        {
            if (string.IsNullOrEmpty(ProviderName) || string.IsNullOrEmpty(DeviceHandle))
            {
                // TODO Log error
                return false;
            }
            if (IsAcquired) return true;
            IsAcquired = true;
            return context.IOController.SubscribeOutput(GetOutputSubscriptionRequest());
        }

        public bool UnsubscribeOutput(Context context)
        {
            if (string.IsNullOrEmpty(ProviderName) || string.IsNullOrEmpty(DeviceHandle))
            {
                // TODO Log error
                return false;
            }
            if (!IsAcquired) return true;
            IsAcquired = false;            
            return context.IOController.UnsubscribeOutput(GetOutputSubscriptionRequest());
        }

        private InputSubscriptionRequest GetInputSubscriptionRequest(DeviceBinding deviceBinding)
        {
            return new InputSubscriptionRequest()
            {
                ProviderDescriptor = GetProviderDescriptor(),
                DeviceDescriptor = GetDeviceDescriptor(),
                SubscriptionDescriptor = GetSubscriptionDescriptor(),
                BindingDescriptor = GetBindingDescriptor(deviceBinding),
                Callback = deviceBinding.Callback
            };
        }

        private OutputSubscriptionRequest GetOutputSubscriptionRequest()
        {
            return new OutputSubscriptionRequest()
            {
                ProviderDescriptor = GetProviderDescriptor(),
                DeviceDescriptor = GetDeviceDescriptor(),
                SubscriptionDescriptor = GetSubscriptionDescriptor()
            };
        }

        private ProviderDescriptor GetProviderDescriptor()
        {
            return new ProviderDescriptor()
            {
                ProviderName = ProviderName
            };
        }

        private DeviceDescriptor GetDeviceDescriptor()
        {
            return new DeviceDescriptor()
            {
                DeviceHandle = DeviceHandle
            };
        }

        private SubscriptionDescriptor GetSubscriptionDescriptor()
        {
            return new SubscriptionDescriptor()
            {
                SubscriberGuid = Guid,
                ProfileGuid = ParentProfile.Guid
            };
        }

        private BindingDescriptor GetBindingDescriptor(DeviceBinding deviceBinding)
        {
            return new BindingDescriptor()
            {
                Type = (BindingType)deviceBinding.KeyType,
                Index = deviceBinding.KeyValue,
                SubIndex = deviceBinding.KeySubValue
            };
        }

        public static List<Device> CopyDeviceList(List<Device> devicelist)
        {
            var newDevicelist = new List<Device>();
            if (devicelist == null) return newDevicelist;

            newDevicelist.AddRange(devicelist);
            return newDevicelist;
        }

        public void SetParentProfile(Profile.Profile profile)
        {
            ParentProfile = profile;
        }

        public List<DeviceBindingNode> GetDeviceBindingMenu(Context context, DeviceIoType type)
        {
            if (DeviceBindingMenu == null || DeviceBindingMenu.Count == 0)
            {
                var ioController = context.IOController;
                var list = type == DeviceIoType.Input
                    ? ioController.GetInputList()
                    : ioController.GetOutputList();
                try
                {
                    DeviceBindingMenu = GetDeviceBindingMenu(list[ProviderName]?.Devices[DeviceHandle]?.Nodes, type);
                }
                catch (KeyNotFoundException)
                {
                    return new List<DeviceBindingNode>
                    {
                        new DeviceBindingNode()
                        {
                            Title = "Device not connected",
                            IsBinding = false
                        }
                    };
                }
            }
            return DeviceBindingMenu;
        }
    }
}