﻿#region Copyright & License Information
/*
 * Copyright 2007-2010 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made 
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation. For more information,
 * see LICENSE.
 */
#endregion

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System;

namespace OpenRA.Network
{
	static class Exts
	{
		public static void Write( this Stream s, byte[] data )
		{
			s.Write( data, 0, data.Length );
		}

		public static byte[] Read( this Stream s, int len )
		{
			var data = new byte[ len ];
			s.Read( data, 0, len );
			return data;
		}

		public static IEnumerable<T> Except<T>( this IEnumerable<T> ts, T t )
		{
			return ts.Except( new[] { t } );
		}

		public static void WriteFrameData(this Stream s, IEnumerable<Order> orders, int frameNumber)
		{
			var bytes = Serialize( orders, frameNumber );
			s.Write( BitConverter.GetBytes( (int)bytes.Length ) );
			s.Write( bytes );
		}

		public static byte[] Serialize( this IEnumerable<Order> orders, int frameNumber )
		{
			var ms = new MemoryStream();
			ms.Write( BitConverter.GetBytes( frameNumber ) );
			foreach( var o in orders.Select( o => o.Serialize() ) )
				ms.Write( o );
			return ms.ToArray();
		}

		public static List<Order> ToOrderList(this byte[] bytes, World world)
		{
			var ms = new MemoryStream(bytes, 4, bytes.Length - 4);
			var reader = new BinaryReader(ms);
			var ret = new List<Order>();
			while( ms.Position < ms.Length )
			{
				var o = Order.Deserialize( world, reader );
				if( o != null )
					ret.Add( o );
			}
			return ret;
		}

		public static byte[] SerializeSync( this List<int> sync, int frameNumber )
		{
			var ms = new MemoryStream();
			using( var writer = new BinaryWriter( ms ) )
			{
				writer.Write( frameNumber );
				writer.Write( (byte)0x65 );
				foreach( var s in sync )
					writer.Write( s );
			}
			return ms.ToArray();
		}
	}
}
