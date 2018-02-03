/* Why this class?
 * 
 * Mostly for similar reasons as with Randomizer.
 */

using System;
using System.Linq;
using System.Text;
using System.IO;
using System.Collections.Generic;
using SysColor = System.Drawing.Color;

namespace Noxico
{
#if DEBUG
	[System.ComponentModel.Editor(typeof(ColorEditor), typeof(System.Drawing.Design.UITypeEditor))]
#endif
	public struct Color
	{
		private static List<Token> colorTable;

		public uint ArgbValue { get; set; }
		public string Name { get; set; }

		static Color()
		{
			colorTable = Mix.GetTokenTree("knowncolors.tml", true);
		}

		/// <summary>
		/// Gets the alpha component value of this Color.
		/// </summary>
		public byte R
		{
			get { return (byte)((this.ArgbValue >> 0x10) & 0xFF); }
		}

		/// <summary>
		/// Gets the green component value of this Color.
		/// </summary>
		public byte G
		{
			get { return (byte)((this.ArgbValue >> 8) & 0xFF); }
		}

		/// <summary>
		/// Gets the blue component value of this Color.
		/// </summary>
		public byte B
		{
			get { return (byte)(this.ArgbValue & 0xFF); }
		}

		/// <summary>
		/// Gets the alpha component value of this Color.
		/// </summary>
		public byte A
		{
			get { return (byte)((this.ArgbValue >> 0x18) & 0xFF); }
		}
		/// <summary>
		/// Gets a value indicating whether this Color is named.
		/// </summary>
		public bool IsNamedColor
		{
			get { return Name != null; }
		}

		//Web colors
		public static Color Transparent { get { return new Color(0x000000, "Transparent"); } }
		public static Color Black { get { return new Color(0xFF000000, "Black"); } }
		public static Color Silver { get { return new Color(0xFFC0C0C0, "Silver"); } }
		public static Color Gray { get { return new Color(0xFF808080, "Gray"); } }
		public static Color White { get { return new Color(0xFFFFFFFF, "White"); } }
		public static Color Maroon { get { return new Color(0xFF800000, "Maroon"); } }
		public static Color Red { get { return new Color(0xFFFF0000, "Red"); } }
		public static Color Purple { get { return new Color(0xFF800080, "Purple"); } }
		public static Color Fuchsia { get { return new Color(0xFFFF00FF, "Fuchsia"); } }
		public static Color Green { get { return new Color(0xFF008000, "Green"); } }
		public static Color Lime { get { return new Color(0xFF00FF00, "Lime"); } }
		public static Color Olive { get { return new Color(0xFF808000, "Olive"); } }
		public static Color Yellow { get { return new Color(0xFFFFFF00, "Yellow"); } }
		public static Color Navy { get { return new Color(0xFF000080, "Navy"); } }
		public static Color Blue { get { return new Color(0xFF0000FF, "Blue"); } }
		public static Color Teal { get { return new Color(0xFF008080, "Teal"); } }
		public static Color Aqua { get { return new Color(0xFF00FFFF, "Aqua"); } }
		public static Color Brown { get { return new Color(0xFF804000, "Brown"); } }
		public static Color Orange { get { return new Color(0xFFFFA500, "Orange"); } }
		public static Color DarkGray { get { return new Color(0xFF404040, "DarkGray"); } }

		/// <summary>
		/// Returns a <see cref="Noxico.Color"/> matching the Color Graphics Adapter palette.
		/// </summary>
		/// <param name="index">The palette index to return a color for.</param>
		/// <returns>a <see cref="Noxico.Color"/> matching the CGA palette for <paramref name="index"/>.</returns>
		public static Color FromCGA(int index)
		{
			if (index < 0 || index > 15)
				throw new ArgumentOutOfRangeException("cgaIndex");
			return Color.FromName("CGA" + index);
			/*
			var r = (2.0 / 3 * (index & 4) / 4 + 1 / 3 * (index & 8)) * 255;
			var g = (2.0 / 3 * (index & 2) / 2 + 1 / 3 * (index & 8)) * 255;
			var b = (2.0 / 3 * (index & 1) / 1 + 1 / 3 * (index & 8)) * 255;
			if (index == 6)
				g /= 2;
			return Color.FromArgb((int)r, (int)g, (int)b);
			*/
		}

		/// <summary>
		/// Returns a <see cref="Noxico.Color"/> matching a CSS-style hex code.
		/// </summary>
		/// <param name="hexCode">A three or six-digit hexadecimal number, with or without a '#' in front.</param>
		/// <returns>the <see cref="Noxico.Color"/> matching the <paramref name="hexCode"/>.</returns>
		public static Color FromCSS(string hexCode)
		{
			if (hexCode.IsBlank())
				throw new ArgumentNullException("hexCode");
			if (hexCode[0] == '#')
				hexCode = hexCode.Substring(1);
			if (hexCode.Length != 6 && hexCode.Length != 3)
				throw new ArgumentException("CSS hexcodes have only three or six digits.");
			if (hexCode.Length == 3)
				hexCode = string.Join(string.Empty, new char[] { hexCode[0], hexCode[0], hexCode[1], hexCode[1], hexCode[2], hexCode[2] });
			var r = int.Parse(hexCode.Substring(0, 2), System.Globalization.NumberStyles.HexNumber);
			var g = int.Parse(hexCode.Substring(2, 2), System.Globalization.NumberStyles.HexNumber);
			var b = int.Parse(hexCode.Substring(4, 2), System.Globalization.NumberStyles.HexNumber);
			return Color.FromArgb(r, g, b);
		}

		public static Color FromArgb(int argb)
		{
			return new Color((uint)argb, null);
		}

		public static Color FromArgb(uint argb)
		{
			return new Color(argb, null);
		}

		public static Color FromArgb(int red, int green, int blue)
		{
			return FromArgb(0xFF, red, green, blue);
		}

		public static Color FromArgb(int alpha, int red, int green, int blue)
		{
			return new Color(MakeArgb((byte)alpha, (byte)red, (byte)green, (byte)blue), null);
		}

		internal Color(uint value, string name)
			: this()
		{
			this.ArgbValue = value;
			this.Name = name;
		}

		/// <summary>
		/// Creates a <see cref="Noxico.Color"/> by looking it up in Noxico's known color list.
		/// Also accepts CSS hexCodes via <see cref="Noxico.Color.FromCSS(string hexCode)"/>.
		/// </summary>
		/// <param name="name">The color to find.</param>
		/// <returns>a <see cref="Noxico.Color"/> matching the specified name if it was found; otherwise, Silver (#C0C0C0).</returns>
		public static Color FromName(string name)
		{
			if (string.IsNullOrEmpty(name))
				return Color.Silver;
			if (name[0] == '#')
				return Color.FromCSS(name);
			var request = name.ToLower().Replace("_", string.Empty).Replace(" ", string.Empty);
			var entry = colorTable.FirstOrDefault(x => x.Name.Equals(request, StringComparison.OrdinalIgnoreCase));
			if (entry == null)
				return Color.Silver;
			return new Color((uint)entry.Value | 0xFF000000, entry.Name); //added the | 0xFF000000 bit to ensure alpha.
		}

		private float Max(float r, float g, float b)
		{
			var M = r;
			if (g > M)
				M = g;
			if (b > M)
				M = b;
			return M;
		}

		private float Min(float r, float g, float b)
		{
			var m = r;
			if (g < m)
				m = g;
			if (b < m)
				m = b;
			return m;
		}

		/// <summary>
		/// Gets the hue-saturation-lightness (HSL) lightness value for this Color structure as a value
		/// from 0.0 to 1.0, where 0.0 represents black and 1.0 represents white.
		/// </summary>
		public float Lightness
		{
			get
			{
				{
					var R = ((float)this.R) / 255f;
					var G = ((float)this.G) / 255f;
					var B = ((float)this.B) / 255f;

					var M = Max(R, G, B);
					var m = Min(R, G, B);

					return ((M + m) / 2f);
				}
			}
		}

		/// <summary>
		/// Gets the hue-saturation-value (HSV) value for this Color structure as a value from 0.0 to
		/// 1.0, where 0.0 represents black and 1.0 represents the pure color.
		/// </summary>
		public float Value
		{
			get
			{
				var R = ((float)this.R) / 255f;
				var G = ((float)this.G) / 255f;
				var B = ((float)this.B) / 255f;

				var M = Max(R, G, B);

				return M;
			}
		}

		/// <summary>
		/// Gets the hue value, in degrees, for this Color structure. The hue is measured in degrees,
		/// ranging from 0° to 360°, with red at 0/360°, green at 120° and blue at 240°.
		/// </summary>
		public float Hue
		{
			get
			{
				{
					if ((this.R == this.G) && (this.G == this.B))
						return 0f;

					var R = ((float)this.R) / 255f;
					var G = ((float)this.G) / 255f;
					var B = ((float)this.B) / 255f;

					var M = Max(R, G, B);
					var m = Min(R, G, B);

					var C = M - m;

					var H = 0f;
					if (R == M)
						H = (G - B) / C;
					else if (G == M)
						H = 2f + ((B - R) / C);
					else if (B == M)
						H = 4f + ((R - G) / C);

					H *= 60f;

					if (H < 0f)
						H += 360f;
					return H;
				}
			}
		}

		/// <summary>
		/// Gets the saturation value for this Color structure as a value from 0.0 to 1.0, where
		/// 0.0 is grayscale and 1.0 is fully saturated.
		/// </summary>
		public float Saturation
		{
			get
			{
				var R = ((float)this.R) / 255f;
				var G = ((float)this.G) / 255f;
				var B = ((float)this.B) / 255f;

				var M = Max(R, G, B);
				var m = Min(R, G, B);

				if (M == m)
					return 0.0f;

				var C = (M + m) / 2f;
				if (C <= 0.5)
					return ((M - m) / (M + m));
				return ((M - m) / ((2f - M) - m));
			}
		}

		private static uint MakeArgb(byte alpha, byte red, byte green, byte blue)
		{
			return (uint)((((red << 0x10) | (green << 8)) | blue) | (alpha << 0x18)) & 0xFFFFFFFF;
		}

		public override string ToString()
		{
			var builder = new StringBuilder(0x20);
			builder.Append(base.GetType().Name);
			builder.Append(" [");
			if (this.Name != null)
				builder.Append(this.Name).Append(", ");
			builder.Append("0x").Append(this.ArgbValue.ToString("X"));
			builder.Append("]");
			return builder.ToString();
		}

		public static bool operator ==(Color left, Color right)
		{
			return (left.ArgbValue == right.ArgbValue);
		}

		public static bool operator !=(Color left, Color right)
		{
			return (left.ArgbValue != right.ArgbValue);
		}

		public override bool Equals(object obj)
		{
			if (obj is Color)
				return (((Color)obj).ArgbValue == this.ArgbValue);
			return false;
		}

		public override int GetHashCode()
		{
			return this.ArgbValue.GetHashCode();
		}

		public static implicit operator SysColor(Color color)
		{
			return SysColor.FromArgb(color.A, color.R, color.G, color.B);
		}

		public static implicit operator Noxico.Color(SysColor color)
		{
			return Color.FromArgb(color.A, color.R, color.G, color.B);
		}

		/// <summary>
		/// Returns the proper name for a color -- "darkslategray", "dark_slate_gray", or "DarkSlateGray" becomes "dark slate gray".
		/// </summary>
		public static string NameColor(string color)
		{
			var req = color.Trim().ToLower().Replace("_", string.Empty).Replace(" ", string.Empty);
			var colorName = string.Empty;
			var entry = colorTable.FirstOrDefault(x => x.Name.Equals(color, StringComparison.OrdinalIgnoreCase));
			if (entry != null)
				colorName = entry.Name;
			else
				return color;
			var ret = new StringBuilder();
			foreach (var c in colorName)
			{
				if (char.IsUpper(c))
					ret.Append(' ');
				ret.Append(c);
			}
			return ret.ToString().Trim().ToLowerInvariant();
		}

		public static Color FromName(Token color)
		{
			if (color == null)
				return Color.FromName("Silver");
			if (color.Name == "color")
				return Color.FromName(color.Text);
			return Color.FromName(color.Name);
		}

		public static string Translate(string color)
		{
			var translated = i18n.GetString("color_" + color.Replace(" ", string.Empty).ToLowerInvariant());
			if (translated[0] == '[')
				return color; //Do NOT return "[color_red]"!
			return translated;
		}

		public static string Translate(Color color)
		{
			if (color.IsNamedColor)
				return Translate(color.Name);
			return string.Empty;
		}

		public string ToHex()
		{
			return string.Format("#{0:X2}{1:X2}{2:X2}", this.R, this.G, this.B);
		}
	}
}
