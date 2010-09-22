#region Copyright & License Information
/*
 * Copyright 2007-2010 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made 
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation. For more information,
 * see LICENSE.
 */
#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using OpenRA.Traits;

namespace OpenRA.GameRules
{
	public static class Footprint
	{
		public static IEnumerable<int2> Tiles( string name, BuildingInfo buildingInfo, int2 topLeft )
		{
			var footprint = buildingInfo.Footprint;
			if( Rules.Info[ name ].Traits.Contains<BibInfo>() )
				footprint += " " + new string( '=', buildingInfo.Dimensions.X );

			return TilesWhere( topLeft, footprint, _ => true );
		}

		public static IEnumerable<int2> Tiles(Actor a)
		{
			return Tiles( a.Info.Name, a.Info.Traits.Get<BuildingInfo>(), a.Location );
		}

		public static IEnumerable<int2> UnpathableTiles( BuildingInfo buildingInfo, int2 position )
		{
			return TilesWhere( position, buildingInfo.Footprint, a => a == 'x' );
		}

		public static IEnumerable<int2> UnpathableTiles( string footprint, int2 position )
		{
			return TilesWhere( position, footprint, a => a == 'x' );
		}

		static IEnumerable<int2> TilesWhere( int2 origin, string footprint, Func<char, bool> cond )
		{
			int2 offset = new int2( 0, 0 );
			for( int i = 0 ; i < footprint.Length ; i++ )
			{
				if( footprint[ i ] == ' ' )
					offset = new int2( 0, offset.Y + 1 );
				else if( footprint[ i ] != '_' )
				{
					if( cond( footprint[ i ] ) )
						yield return origin + offset;
					++offset.X;
				}
				else
					++offset.X;
			}
		}

		public static int2 AdjustForBuildingSize( BuildingInfo buildingInfo )
		{
			var dim = buildingInfo.Dimensions;
			return new int2( dim.X / 2, dim.Y > 1 ? ( dim.Y + 1 ) / 2 : 0 );
		}
	}
}
