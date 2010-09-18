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
	}
}
