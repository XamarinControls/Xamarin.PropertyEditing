using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Xamarin.PropertyEditing.Reflection;
using Xamarin.PropertyEditing.Tests.MockControls;

namespace Xamarin.PropertyEditing.Tests
{
	internal class MockNameableEditor :MockObjectEditor, INameableObject
	{
		public string ObjectName { get; set; } = "Nameable";

		public Task<string> GetNameAsync ()
		{
			return Task.FromResult (ObjectName);
		}

		public Task SetNameAsync (string name)
		{
			ObjectName = name;
			return Task.FromResult (false);
		}

		public MockNameableEditor (MockControl control) : base (control)
		{ }
	}

	internal class MockObjectEditor
		: IObjectEditor, IObjectEventEditor
	{
		public MockObjectEditor ()
		{
		}

		public MockObjectEditor (params IPropertyInfo[] properties)
		{
			Properties = properties.ToArray ();
		}

		public MockObjectEditor (IReadOnlyList<IPropertyInfo> properties,
			IReadOnlyDictionary<IPropertyInfo, IReadOnlyList<ITypeInfo>> assignableTypes)
		{
			Properties = properties;
			this.assignableTypes = assignableTypes;
		}

		public MockObjectEditor (MockControl control)
		{
			Properties = control.Properties.Values.ToArray();
			Events = control.Events.Values.ToArray();
			Target = control;
			TypeName = control.GetType ().Name;
		}

		public object Target
		{
			get;
			set;
		} = new object ();

		public string TypeName
		{
			get;
			set;
		}

		public event EventHandler<EditorPropertyChangedEventArgs> PropertyChanged;

		public Func<IPropertyInfo, object, object, object> ValueEvaluator
		{
			get;
			set;
		}

		public IReadOnlyCollection<IPropertyInfo> Properties
		{
			get;
			set;
		} = new IPropertyInfo[0];

		public IReadOnlyCollection<IEventInfo> Events
		{
			get;
			set;
		} = new IEventInfo[0];

		public IObjectEditor Parent
		{
			get;
			set;
		}

		public IReadOnlyList<IObjectEditor> DirectChildren
		{
			get;
			set;
		}

		public void ChangeAllProperties ()
		{
			PropertyChanged?.Invoke (this, new EditorPropertyChangedEventArgs (null));
		}

		public void RaisePropertyChanged (IPropertyInfo property)
		{
			PropertyChanged?.Invoke (this, new EditorPropertyChangedEventArgs (property));
		}

		public Task AttachHandlerAsync (IEventInfo ev, string handlerName)
		{
			this.events[ev] = handlerName;
			return Task.FromResult (true);
		}

		public Task DetachHandlerAsync (IEventInfo ev, string handlerName)
		{
			this.events.Remove (ev);
			return Task.FromResult (true);
		}

		public Task<IReadOnlyList<string>> GetHandlersAsync (IEventInfo ev)
		{
			string handler;
			if (this.events.TryGetValue (ev, out handler))
				return Task.FromResult<IReadOnlyList<string>> (new[] { handler });

			return Task.FromResult<IReadOnlyList<string>> (new string[0]);
		}

		public Task<AssignableTypesResult> GetAssignableTypesAsync (IPropertyInfo property, bool childTypes)
		{
			if (this.assignableTypes == null) {
				return ReflectionObjectEditor.GetAssignableTypes (property, childTypes);
			} else if (!this.assignableTypes.TryGetValue (property, out IReadOnlyList<ITypeInfo> types))
				return Task.FromResult (new AssignableTypesResult (Enumerable.Empty<ITypeInfo> ().ToArray ()));
			else
				return Task.FromResult (new AssignableTypesResult (types));
		}

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
		public async Task SetValueAsync<T> (IPropertyInfo property, ValueInfo<T> value, PropertyVariation variation = null)
		{
			if (variation != null)
				throw new NotSupportedException(); // TODO

			value = new ValueInfo<T> {
				CustomExpression = value.CustomExpression,
				Source = value.Source,
				ValueDescriptor = value.ValueDescriptor,
				SourceDescriptor = value.SourceDescriptor,
				Value = value.Value
			};

			if (value.Source != ValueSource.Local && ValueEvaluator != null) {
				value.Value = (T)ValueEvaluator (property, value.ValueDescriptor, value.SourceDescriptor);
			} else if (value.Source == ValueSource.Unset || (property.ValueSources.HasFlag (ValueSources.Default) && Equals (value.Value, default(T))) && value.ValueDescriptor == null) {
				this.values.Remove (property);
				PropertyChanged?.Invoke (this, new EditorPropertyChangedEventArgs (property));
				return;
			}

			object softValue = value;

			if (typeof(T) != property.Type) {
				IPropertyConverter converter = property as IPropertyConverter;

				object v;
				if (converter != null && converter.TryConvert (value.Value, property.Type, out v)) {
					var softType = typeof(ValueInfo<>).MakeGenericType (property.Type);
					softValue = Activator.CreateInstance (softType);
					softType.GetProperty ("Value").SetValue (softValue, v);
					softType.GetProperty ("ValueDescriptor").SetValue (softValue, value.ValueDescriptor);
					softType.GetProperty ("Source").SetValue (softValue, value.Source);
					softType.GetProperty ("SourceDescriptor").SetValue (softValue, value.SourceDescriptor);
				}

				if (typeof(T).Name == "IReadOnlyList`1") {
					var list = (IReadOnlyList<int>) value.Value;
					int iv = 0;
					foreach (int flag in list) {
						iv |= flag;
					}

					softValue = new ValueInfo<int> {
						Value = iv,
						Source = value.Source
					};
				}
			}

			// Set to resource won't pass values so we will store it on the info since we just pass it back in GetValue
			if (value.Source == ValueSource.Resource && value.SourceDescriptor is Resource) {
				Type rt = value.SourceDescriptor.GetType();
				if (rt.IsGenericType) {
					Type ta = rt.GetGenericArguments ()[0];
					if (typeof (T).IsAssignableFrom (ta)) {
						PropertyInfo pi = rt.GetProperty ("Value");
						value.Value = (T)pi.GetValue (value.SourceDescriptor);
					} else {
						TypeConverter converter = TypeDescriptor.GetConverter (ta);
						if (converter != null && converter.CanConvertTo(typeof(T))) {
							PropertyInfo pi = rt.GetProperty ("Value");
							value.Value = (T)converter.ConvertTo (pi.GetValue (value.SourceDescriptor), typeof (T));
						}
					}
				}
			}
			
			this.values[property] = softValue;
			PropertyChanged?.Invoke (this, new EditorPropertyChangedEventArgs (property));
		}

		public async Task<ValueInfo<T>> GetValueAsync<T> (IPropertyInfo property, PropertyVariation variation = null)
		{
			if (variation != null)
				throw new NotSupportedException (); // TODO

			Type tType = typeof(T);

			object value;
			if (this.values.TryGetValue (property, out value)) {
				var info = value as ValueInfo<T>;
				if (info != null) {
					return new ValueInfo<T> {
						CustomExpression = info.CustomExpression,
						Source = info.Source,
						ValueDescriptor = info.ValueDescriptor,
						SourceDescriptor = info.SourceDescriptor,
						Value = info.Value
					};
				} else if (value == null || value is T) {
					return new ValueInfo<T> {
						Value = (T) value,
						Source = ValueSource.Local
					};
				} else if (tType.Name == "IReadOnlyList`1") {
					// start with just supporting ints for now
					var predefined = (IReadOnlyDictionary<string, int>)property.GetType().GetProperty(nameof(IHavePredefinedValues<int>.PredefinedValues)).GetValue(property);

					var underlyingInfo = value as ValueInfo<int>;

					int realValue;
					if (value is int i) {
						realValue = i;
					} else
						realValue = ((ValueInfo<int>) value).Value;

					var flags = new List<int> ();
					foreach (int v in predefined.Values) {
						if (v == 0 && realValue != 0)
							continue;

						if ((realValue & v) == v)
							flags.Add (v);
					}

					return (ValueInfo<T>)Convert.ChangeType(new ValueInfo<IReadOnlyList<int>> {
						Value = flags,
						Source = underlyingInfo?.Source ?? ValueSource.Local
					}, typeof(ValueInfo<T>));
				} else {
					object sourceDescriptor = null, valueDescriptor = null;
					ValueSource source = ValueSource.Local;
					Type valueType = value.GetType ();
					if (valueType.IsConstructedGenericType && valueType.GetGenericTypeDefinition () == typeof(ValueInfo<>)) {
						source = (ValueSource)valueType.GetProperty ("Source").GetValue (value);
						sourceDescriptor = valueType.GetProperty (nameof (ValueInfo<T>.SourceDescriptor)).GetValue (value);
						valueDescriptor = valueType.GetProperty (nameof (ValueInfo<T>.ValueDescriptor)).GetValue (value);
						value = valueType.GetProperty ("Value").GetValue (value);
						valueType = valueType.GetGenericArguments ()[0];
					}

					object newValue;
					IPropertyConverter converter = property as IPropertyConverter;
					if (converter != null && converter.TryConvert (value, tType, out newValue)) {
						return new ValueInfo<T> {
							Source = source,
							Value = (T)newValue,
							ValueDescriptor = valueDescriptor,
							SourceDescriptor = sourceDescriptor
						};
					} else if (typeof(T).IsAssignableFrom (valueType)) {
						return new ValueInfo<T> {
							Source = source,
							Value = (T)value,
							ValueDescriptor = valueDescriptor,
							SourceDescriptor = sourceDescriptor
						};
					}
				}
			}

			return new ValueInfo<T> {
				Source = (property.ValueSources.HasFlag (ValueSources.Default)) ? ValueSource.Default : ValueSource.Unset,
				Value = default(T)
			};
		}
#pragma warning restore CS1998

		internal readonly IDictionary<IPropertyInfo, object> values = new Dictionary<IPropertyInfo, object> ();
		internal readonly IDictionary<IEventInfo, string> events = new Dictionary<IEventInfo, string> ();
		internal readonly IReadOnlyDictionary<IPropertyInfo, IReadOnlyList<ITypeInfo>> assignableTypes;
	}
}