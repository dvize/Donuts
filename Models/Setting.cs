using System;
using System.Collections.Generic;

namespace Donuts.Models
{
    internal class Setting<T>
    {
        public string Name
        {
            get; set;
        }
        public string TooltipText
        {
            get; set;
        }
        public T Value
        {
            get; set;
        }
        public T DefaultValue
        {
            get; set;
        }
        public T MinValue
        {
            get; set;
        }
        public T MaxValue
        {
            get; set;
        }
        public List<T> Options
        {
            get; set;
        }

        // Constructor to handle regular settings
        public Setting(string name, string tooltipText, T value, T defaultValue, T minValue = default(T), T maxValue = default(T), List<T> options = null)
        {
            Name = name;
            TooltipText = tooltipText;
            Value = value;
            DefaultValue = defaultValue;
            MinValue = minValue;
            MaxValue = maxValue;
            Options = options ?? new List<T>();
        }

        // Constructor to handle list/array settings with a single default value
        public Setting(string name, string tooltipText, T value, string defaultValue, T minValue = default(T), T maxValue = default(T), List<T> options = null)
        {
            Name = name;
            TooltipText = tooltipText;
            Value = value;

            if (typeof(T).IsArray)
            {
                DefaultValue = (T)(object)new string[] { defaultValue };
            }
            else if (typeof(IList<string>).IsAssignableFrom(typeof(T)))
            {
                DefaultValue = (T)(object)new List<string> { defaultValue };
            }
            else
            {
                DefaultValue = (T)(object)defaultValue;
            }

            MinValue = minValue;
            MaxValue = maxValue;
            Options = options ?? new List<T>();
        }
    }
}
