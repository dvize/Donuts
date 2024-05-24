using System;
using System.Collections.Generic;
using System.Text;

namespace Donuts.Models
{
    internal class Setting<T>
    {
        private string toolTipText;

        public string Name
        {
            get; set;
        }

        public string ToolTipText
        {
            get => toolTipText;
            set => toolTipText = InsertCarriageReturns(value, 50);
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
        public T[] Options
        {
            get; set;
        }

        private bool hasLoggedError = false;

        // Constructor to handle regular settings
        public Setting(string name, string tooltipText, T value, T defaultValue, T minValue = default(T), T maxValue = default(T), T[] options = null)
        {
            Name = name;
            ToolTipText = tooltipText;
            Value = value;
            DefaultValue = defaultValue;
            MinValue = minValue;
            MaxValue = maxValue;
            Options = options ?? new T[0];
        }

        public bool LogErrorOnceIfOptionsInvalid()
        {
            if (Options == null || Options.Length == 0)
            {
                if (!hasLoggedError)
                {
                    DonutsPlugin.Logger.LogError($"Dropdown setting '{Name}' has an uninitialized or empty options list.");
                    hasLoggedError = true;
                }
                return true;
            }
            return false;
        }

        private string InsertCarriageReturns(string text, int maxLength)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
            {
                return text;
            }

            StringBuilder formattedText = new StringBuilder();
            int start = 0;

            while (start < text.Length)
            {
                int end = Math.Min(start + maxLength, text.Length);
                if (end < text.Length && text[end] != ' ')
                {
                    int lastSpace = text.LastIndexOf(' ', end, end - start);
                    if (lastSpace > start)
                    {
                        end = lastSpace;
                    }
                }

                formattedText.Append(text.Substring(start, end - start).Trim());
                formattedText.AppendLine();
                start = end + 1;
            }

            return formattedText.ToString().Trim();
        }
    }
}
