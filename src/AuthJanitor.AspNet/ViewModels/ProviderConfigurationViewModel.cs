// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AuthJanitor.UI.Shared.ViewModels
{
    public class ProviderConfigurationViewModel : IAuthJanitorViewModel
    {
        public IEnumerable<ProviderConfigurationItemViewModel> ConfigurationItems { get; set; } = new List<ProviderConfigurationItemViewModel>();

        [JsonIgnore]
        [Newtonsoft.Json.JsonIgnore]
        public string SerializedConfiguration
        {
            get
            {
                var dict = new Dictionary<string, object>();
                foreach (var item in ConfigurationItems)
                {
                    switch (item.InputType)
                    {
                        case ProviderConfigurationItemViewModel.InputTypes.Boolean:
                            dict.Add(item.Name, item.BoolValue);
                            break;
                        case ProviderConfigurationItemViewModel.InputTypes.Enumeration:
                            dict.Add(item.Name, item.IntValue);
                            break;
                        case ProviderConfigurationItemViewModel.InputTypes.Integer:
                            dict.Add(item.Name, item.IntValue);
                            break;
                        case ProviderConfigurationItemViewModel.InputTypes.Text:
                            dict.Add(item.Name, item.Value);
                            break;
                        case ProviderConfigurationItemViewModel.InputTypes.TextArray:
                            dict.Add(item.Name, item.Value); // TODO: Check this
                            break;
                    }
                }
                return JsonSerializer.Serialize(dict);
            }
            set
            {
                var deserialized = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(value);
                foreach (var item in ConfigurationItems)
                {
                    System.Console.WriteLine($"{item.Name} => {deserialized[item.Name]} ({item.InputType})");
                    if (deserialized.ContainsKey(item.Name))
                    {
                        switch (item.InputType)
                        {
                            case ProviderConfigurationItemViewModel.InputTypes.Text:
                                item.Value = deserialized[item.Name].GetString();
                                break;
                            case ProviderConfigurationItemViewModel.InputTypes.TextArray:
                                // TODO: Fix this
                                item.Value = deserialized[item.Name].GetString();
                                break;
                            case ProviderConfigurationItemViewModel.InputTypes.Integer:
                                item.IntValue = deserialized[item.Name].GetInt32();
                                break;
                            case ProviderConfigurationItemViewModel.InputTypes.Boolean:
                                item.BoolValue = deserialized[item.Name].GetBoolean();
                                break;
                            case ProviderConfigurationItemViewModel.InputTypes.Enumeration:
                                item.IntValue = deserialized[item.Name].GetInt32();
                                break;
                        }
                    }
                }
            }
        }
    }
}
