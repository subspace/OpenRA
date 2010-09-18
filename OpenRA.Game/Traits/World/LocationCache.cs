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
using System.Text;

namespace OpenRA.Traits
{
	public class LocationCacheInfo : ITraitInfo
	{
		public object Create( ActorInitializer init ) { return new LocationCache( init ); }
	}

	public class LocationCache
	{
		public readonly UnitInfluence uim;
		public readonly BuildingInfluence bim;

		public LocationCache( ActorInitializer init )
		{
			uim = new UnitInfluence( init.world );
			bim = new BuildingInfluence( init.world );
		}

		public bool AnyActorsAt( int2 location )
		{
			if( bim.GetBuildingAt( location ) != null ) return true;
			if( uim.GetUnitsAt( location ).Any() ) return true;

			return false;
		}

		public IEnumerable<Actor> ActorsAt( int2 location )
		{
			var b = bim.GetBuildingAt( location );
			if( b != null )
				yield return b;

			foreach( var u in uim.GetUnitsAt( location ) )
				yield return u;
		}

		public IEnumerable<Actor> ActorsBlocking( int2 location )
		{
			var b = bim.GetBuildingBlocking( location );
			if( b != null )
				yield return b;

			foreach( var u in uim.GetUnitsAt( location ) )
				yield return u;
		}
	}
}
