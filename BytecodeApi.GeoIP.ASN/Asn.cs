﻿using System;

namespace BytecodeApi.GeoIP.ASN
{
	/// <summary>
	/// Represents an Autonomous System Number with an organization name.
	/// </summary>
	public sealed class Asn : IEquatable<Asn>
	{
		/// <summary>
		/// Gets the Autonomous System Number.
		/// <para>Example: 46164</para>
		/// </summary>
		public int Number { get; private set; }
		/// <summary>
		/// Gets the name of the Autonomous System Organization.
		/// <para>Example: AT&amp;T Mobility LLC</para>
		/// </summary>
		public string Organization { get; private set; }

		internal Asn(int number, string organization)
		{
			Number = number;
			Organization = organization;
		}

		/// <summary>
		/// Returns a <see cref="string" /> that represents this instance.
		/// </summary>
		/// <returns>
		/// A <see cref="string" /> that represents this instance.
		/// </returns>
		public override string ToString()
		{
			return "[" + Number + ", " + Organization + "]";
		}
		/// <summary>
		/// Determines whether the specified <see cref="object" /> is equal to this instance.
		/// </summary>
		/// <param name="obj">The <see cref="object" /> to compare with this instance.</param>
		/// <returns>
		/// <see langword="true" />, if the specified <see cref="object" /> is equal to this instance;
		/// otherwise, <see langword="false" />.
		/// </returns>
		public override bool Equals(object obj)
		{
			return obj is Asn asn && Equals(asn);
		}
		/// <summary>
		/// Determines whether this instance is equal to another <see cref="Asn" />.
		/// </summary>
		/// <param name="other">The <see cref="Asn" /> to compare to this instance.</param>
		/// <returns>
		/// <see langword="true" />, if this instance is equal to the <paramref name="other" /> parameter;
		/// otherwise, <see langword="false" />.
		/// </returns>
		public bool Equals(Asn other)
		{
			return Number == other.Number;
		}
		/// <summary>
		/// Returns a hash code for this <see cref="Asn" />.
		/// </summary>
		/// <returns>
		/// The hash code for this <see cref="Asn" /> instance.
		/// </returns>
		public override int GetHashCode()
		{
			return Number.GetHashCode();
		}

		/// <summary>
		/// Compares two <see cref="Asn" /> instances for equality.
		/// </summary>
		/// <param name="a">The first <see cref="Asn" /> to compare.</param>
		/// <param name="b">The second <see cref="Asn" /> to compare.</param>
		/// <returns>
		/// <see langword="true" />, if both <see cref="Asn" /> are equal;
		/// otherwise, <see langword="false" />.
		/// </returns>
		public static bool operator ==(Asn a, Asn b)
		{
			return Equals(a, b);
		}
		/// <summary>
		/// Compares two <see cref="Asn" /> instances for inequality.
		/// </summary>
		/// <param name="a">The first <see cref="Asn" /> to compare.</param>
		/// <param name="b">The second <see cref="Asn" /> to compare.</param>
		/// <returns>
		/// <see langword="true" />, if both <see cref="Asn" /> are not equal;
		/// otherwise, <see langword="false" />.
		/// </returns>
		public static bool operator !=(Asn a, Asn b)
		{
			return !(a == b);
		}
	}
}