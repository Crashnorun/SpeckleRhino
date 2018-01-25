﻿using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Rhino.DocObjects;
using Rhino.Geometry;
using SpeckleCore;
using SpeckleRhinoConverter;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading.Tasks;

namespace SpeckleRhino
{
  /// <summary>
  /// Class that holds a rhino receiver client warpped around the
  /// SpeckleApiClient.
  /// </summary>
  [Serializable]
  public class RhinoReceiver : ISpeckleRhinoClient
  {
    public Interop Context { get; set; }

    public SpeckleApiClient Client { get; set; }

    public List<SpeckleObject> Objects { get; set; }

    public SpeckleDisplayConduit Display;

    public string StreamId { get; private set; }

    public bool Paused { get; set; } = false;

    public bool Visible { get; set; } = true;

    public RhinoReceiver( string _payload, Interop _parent )
    {
      Context = _parent;
      dynamic payload = JsonConvert.DeserializeObject( _payload );

      StreamId = ( string ) payload.streamId;

      Client = new SpeckleApiClient( ( string ) payload.account.restApi, new RhinoConverter(), true );

      Client.OnReady += Client_OnReady;
      Client.OnLogData += Client_OnLogData;
      Client.OnWsMessage += Client_OnWsMessage;
      Client.OnError += Client_OnError;

      Client.IntializeReceiver( ( string ) payload.streamId, Context.GetDocumentName(), "Rhino", Context.GetDocumentGuid(), ( string ) payload.account.apiToken );

      Display = new SpeckleDisplayConduit();
      Display.Enabled = true;

      Objects = new List<SpeckleObject>();
    }

    #region events
    private void Client_OnError( object source, SpeckleEventArgs e )
    {
      Context.NotifySpeckleFrame( "client-error", StreamId, JsonConvert.SerializeObject( e.EventData ) );
    }

    public virtual void Client_OnLogData( object source, SpeckleEventArgs e )
    {
      Context.NotifySpeckleFrame( "client-log", StreamId, JsonConvert.SerializeObject( e.EventData ) );
    }

    public virtual void Client_OnReady( object source, SpeckleEventArgs e )
    {
      Context.NotifySpeckleFrame( "client-add", StreamId, JsonConvert.SerializeObject( new { stream = Client.Stream, client = Client } ) );

      Context.AddClientToStore( this );
      //Context.UserClients.Add(this);

      UpdateGlobal();
    }

    public virtual void Client_OnWsMessage( object source, SpeckleEventArgs e )
    {
      if ( Paused )
      {
        Context.NotifySpeckleFrame( "client-expired", StreamId, "" );
        return;
      }

      switch ( ( string ) e.EventObject.args.eventType )
      {
        case "update-global":
          UpdateGlobal();
          break;
        case "update-meta":
          UpdateMeta();
          break;
        case "update-name":
          UpdateName();
          break;
        case "update-object":
          break;
        case "update-children":
          UpdateChildren();
          break;
        default:
          Context.NotifySpeckleFrame( "client-log", StreamId, JsonConvert.SerializeObject( "Unkown event: " + ( string ) e.EventObject.args.eventType ) );
          break;
      }
    }
    #endregion

    #region updates

    public void UpdateName( )
    {
      var response = Client.StreamGetNameAsync( StreamId );
      Client.Stream.Name = response.Result.Name;
      Context.NotifySpeckleFrame( "client-metadata-update", StreamId, Client.Stream.ToJson() ); // i'm lazy
    }

    public void UpdateMeta( )
    {
      Context.NotifySpeckleFrame( "client-log", StreamId, JsonConvert.SerializeObject( "Metadata update received." ) );

      var streamGetResponse = Client.StreamGet( StreamId );
      if ( streamGetResponse.Success == false )
      {
        Context.NotifySpeckleFrame( "client-error", StreamId, streamGetResponse.Message );
        Context.NotifySpeckleFrame( "client-log", StreamId, JsonConvert.SerializeObject( "Failed to retrieve global update." ) );
      }

      Client.Stream = streamGetResponse.Stream;

      Context.NotifySpeckleFrame( "client-metadata-update", StreamId, Client.Stream.ToJson() );

    }

    public void UpdateGlobal( )
    {
      Context.NotifySpeckleFrame( "client-log", StreamId, JsonConvert.SerializeObject( "Global update received." ) );

      var streamGetResponse = Client.StreamGet( StreamId );
      if ( streamGetResponse.Success == false )
      {
        Context.NotifySpeckleFrame( "client-error", StreamId, streamGetResponse.Message );
        Context.NotifySpeckleFrame( "client-log", StreamId, JsonConvert.SerializeObject( "Failed to retrieve global update." ) );
      }

      Client.Stream = streamGetResponse.Stream;
      var COPY = Client.Stream;
      Context.NotifySpeckleFrame( "client-metadata-update", StreamId, Client.Stream.ToJson() );
      Context.NotifySpeckleFrame( "client-is-loading", StreamId, "" );

      // prepare payload
      PayloadObjectGetBulk payload = new PayloadObjectGetBulk();
      payload.Objects = Client.Stream.Objects.Where( o => !Context.ObjectCache.ContainsKey( o ) );

      // bug in speckle core, no sync method for this :(
      Client.ObjectGetBulkAsync( "omit=displayValue", payload ).ContinueWith( tres =>
         {
           if ( tres.Result.Success == false )
             Context.NotifySpeckleFrame( "client-error", StreamId, streamGetResponse.Message );
           var copy = tres.Result;

              // add to cache
              foreach ( var obj in tres.Result.Objects )
             Context.ObjectCache[ obj.DatabaseId ] = obj;

              // populate real objects
              Objects.Clear();
           foreach ( var objId in Client.Stream.Objects )
             Objects.Add( Context.ObjectCache[ objId ] );

           DisplayContents();
           Context.NotifySpeckleFrame( "client-done-loading", StreamId, "" );
         } );

    }

    public void UpdateChildren( )
    {
      var getStream = Client.StreamGet( StreamId );
      Client.Stream = getStream.Stream;

      Context.NotifySpeckleFrame( "client-children", StreamId, Client.Stream.ToJson() );
    }
    #endregion

    #region display & helpers
    public void DisplayContents( )
    {
      RhinoConverter rhinoConverter = new RhinoConverter();

      Display.Geometry = new List<GeometryBase>();
      Display.Colors = new List<System.Drawing.Color>();
      Display.VisibleList = new List<bool>();

      int count = 0;
      foreach ( SpeckleObject myObject in Objects )
      {
        var gb = rhinoConverter.ToNative( myObject );

        Display.Colors.Add( GetColorFromLayer( GetLayerFromIndex( count ) ) );

        Display.VisibleList.Add( true );

        if ( gb is GeometryBase )
        {
          Display.Geometry.Add( gb as GeometryBase );
        }
        else
        {
          Display.Geometry.Add( null );
        }

        count++;
      }

      Rhino.RhinoDoc.ActiveDoc.Views.Redraw();
    }

    public SpeckleLayer GetLayerFromIndex( int index )
    {
      return Client.Stream.Layers.FirstOrDefault( layer => ( ( index >= layer.StartIndex ) && ( index < layer.StartIndex + layer.ObjectCount ) ) );
    }

    public System.Drawing.Color GetColorFromLayer( SpeckleLayer layer )
    {
      System.Drawing.Color layerColor = System.Drawing.ColorTranslator.FromHtml( "#AEECFD" );
      try
      {
        if ( layer != null && layer.Properties != null )
          layerColor = System.Drawing.ColorTranslator.FromHtml( layer.Properties.Color.Hex );
      }
      catch
      {
        Debug.WriteLine( "Layer '{0}' had no assigned color", layer.Name );
      }
      return layerColor;
    }

    public string GetClientId( )
    {
      return Client.ClientId;
    }

    public ClientRole GetRole( )
    {
      return ClientRole.Receiver;
    }
    #endregion

    #region Bake

    public void Bake( )
    {
      string parent = String.Format( "{0} | {1}", Client.Stream.Name, Client.Stream.StreamId );
#if WINR6
            var parentId = Rhino.RhinoDoc.ActiveDoc.Layers.FindByFullPath(parent, -1);
#elif WINR5
      var parentId = Rhino.RhinoDoc.ActiveDoc.Layers.FindByFullPath( parent, true );
#endif

      if ( parentId == -1 )
      {

        //There is no layer in the document with the Stream Name | Stream Id as a name, so create one

        var parentLayer = new Layer()
        {
          Color = System.Drawing.Color.Black,
          Name = parent
        };

        //Maybe could be useful in the future
        parentLayer.SetUserString( "spk", Client.Stream.StreamId );

        parentId = Rhino.RhinoDoc.ActiveDoc.Layers.Add( parentLayer );
      }
      else

        //Layer with this name does exist. 
        //This is either a coincidence or a receiver has affected this file before.
        //In any case, delete any sublayers and any objects within them.

        foreach ( var layer in Rhino.RhinoDoc.ActiveDoc.Layers[ parentId ].GetChildren() )
          Rhino.RhinoDoc.ActiveDoc.Layers.Purge( layer.LayerIndex, false );

      foreach ( var spkLayer in Client.Stream.Layers )
      {
#if WINR6
                var layerId = Rhino.RhinoDoc.ActiveDoc.Layers.FindByFullPath(parent + "::" + spkLayer.Name, -1);
#elif WINR5
        var layerId = Rhino.RhinoDoc.ActiveDoc.Layers.FindByFullPath( parent + "::" + spkLayer.Name, true );
#endif

        //This is always going to be the case. 

        if ( layerId == -1 )
        {

          var index = -1;

          if ( spkLayer.Name.Contains( "::" ) )
          {
            var spkLayerPath = spkLayer.Name.Split( new string[ ] { "::" }, StringSplitOptions.None );

            var parentLayerId = Guid.Empty;

            foreach ( var layerPath in spkLayerPath )
            {

              if ( parentLayerId == Guid.Empty )
                parentLayerId = Rhino.RhinoDoc.ActiveDoc.Layers[ parentId ].Id;

              var layer = new Layer()
              {
                Name = layerPath,
                ParentLayerId = parentLayerId,
                Color = GetColorFromLayer( spkLayer ),
                IsVisible = true
              };

              var parentLayerName = Rhino.RhinoDoc.ActiveDoc.Layers.First( l => l.Id == parentLayerId ).FullPath;
#if WINR6
                            var layerExist = Rhino.RhinoDoc.ActiveDoc.Layers.FindByFullPath(parentLayerName + "::" + layer.Name, -1);
#elif WINR5
              var layerExist = Rhino.RhinoDoc.ActiveDoc.Layers.FindByFullPath( parentLayerName + "::" + layer.Name, true );
#endif

              if ( layerExist == -1 )
              {
                index = Rhino.RhinoDoc.ActiveDoc.Layers.Add( layer );
                parentLayerId = Rhino.RhinoDoc.ActiveDoc.Layers[ index ].Id;
              }
              else
              {
                parentLayerId = Rhino.RhinoDoc.ActiveDoc.Layers[ layerExist ].Id;
              }

            }
          }
          else
          {

            var layer = new Layer()
            {
              Name = spkLayer.Name,
              Id = Guid.Parse( spkLayer.Guid ),
              ParentLayerId = Rhino.RhinoDoc.ActiveDoc.Layers[ parentId ].Id,
              Color = GetColorFromLayer( spkLayer ),
              IsVisible = true
            };

            index = Rhino.RhinoDoc.ActiveDoc.Layers.Add( layer );
          }

          for ( int i = ( int ) spkLayer.StartIndex; i < spkLayer.StartIndex + spkLayer.ObjectCount; i++ )
          {
            if ( Display.Geometry[ i ] != null && !Display.Geometry[ i ].IsDocumentControlled )
            {
              Rhino.RhinoDoc.ActiveDoc.Objects.Add( Display.Geometry[ i ], new ObjectAttributes() { LayerIndex = index } );
            }
          }
        }
      }

      Rhino.RhinoDoc.ActiveDoc.Views.Redraw();
    }

    public void BakeLayer( string layerId )
    {
      SpeckleLayer myLayer = Client.Stream.Layers.FirstOrDefault( l => l.Guid == layerId );

      // create or get parent
      string parent = String.Format( "{1} | {0}", Client.Stream.StreamId, Client.Stream.Name );

      var parentId = Rhino.RhinoDoc.ActiveDoc.Layers.FindByFullPath( parent, true );
      if ( parentId == -1 )
      {
        var parentLayer = new Layer()
        {
          Color = System.Drawing.Color.Black,
          Name = parent
        };
        parentId = Rhino.RhinoDoc.ActiveDoc.Layers.Add( parentLayer );
      }
      else
      {
        int prev = Rhino.RhinoDoc.ActiveDoc.Layers.FindByFullPath( parent + "::" + myLayer.Name, true );
        if ( prev != -1 )
          Rhino.RhinoDoc.ActiveDoc.Layers.Purge( prev, true );
      }

      int theLayerId = Rhino.RhinoDoc.ActiveDoc.Layers.FindByFullPath( parent + "::" + myLayer.Name, true );
      if ( theLayerId == -1 )
      {
        var layer = new Layer()
        {
          Name = myLayer.Name,
          Id = Guid.Parse( myLayer.Guid ),
          ParentLayerId = Rhino.RhinoDoc.ActiveDoc.Layers[ parentId ].Id,
          Color = GetColorFromLayer( myLayer ),
          IsVisible = true
        };
        var index = Rhino.RhinoDoc.ActiveDoc.Layers.Add( layer );
        for ( int i = ( int ) myLayer.StartIndex; i < myLayer.StartIndex + myLayer.ObjectCount; i++ )
        {
          if ( Display.Geometry[ i ] != null )
          {
            Rhino.RhinoDoc.ActiveDoc.Objects.Add( Display.Geometry[ i ], new ObjectAttributes() { LayerIndex = index } );
          }
        }
      }
      Rhino.RhinoDoc.ActiveDoc.Views.Redraw();
    }

    #endregion

    #region Toggles

    public void TogglePaused( bool status )
    {
      this.Paused = status;
    }

    public void ToggleVisibility( bool status )
    {
      this.Display.Enabled = status;
      Rhino.RhinoDoc.ActiveDoc.Views.Redraw();
    }

    public void ToggleLayerHover( string layerId, bool status )
    {
      SpeckleLayer myLayer = Client.Stream.Layers.FirstOrDefault( l => l.Guid == layerId );
      if ( myLayer == null ) throw new Exception( "Bloopers. Layer not found." );

      if ( status )
      {
        Display.HoverRange = new Interval( ( double ) myLayer.StartIndex, ( double ) ( myLayer.StartIndex + myLayer.ObjectCount ) );
      }
      else
        Display.HoverRange = null;
      Rhino.RhinoDoc.ActiveDoc.Views.Redraw();
    }

    public void ToggleLayerVisibility( string layerId, bool status )
    {
      SpeckleLayer myLayer = Client.Stream.Layers.FirstOrDefault( l => l.Guid == layerId );
      if ( myLayer == null ) throw new Exception( "Bloopers. Layer not found." );

      for ( int i = ( int ) myLayer.StartIndex; i < myLayer.StartIndex + myLayer.ObjectCount; i++ )
        Display.VisibleList[ i ] = status;
      Rhino.RhinoDoc.ActiveDoc.Views.Redraw();
    }
    #endregion

    #region serialisation & end of life

    public void Dispose( bool delete = false )
    {
      Client.Dispose( delete );
      Display.Enabled = false;
      Rhino.RhinoDoc.ActiveDoc.Views.Redraw();
    }

    public void Dispose( )
    {
      Client.Dispose();
      Display.Enabled = false;
      Rhino.RhinoDoc.ActiveDoc.Views.Redraw();
    }

    protected RhinoReceiver( SerializationInfo info, StreamingContext context )
    {
      JsonConvert.DefaultSettings = ( ) => new JsonSerializerSettings()
      {
        ContractResolver = new CamelCasePropertyNamesContractResolver()
      };

      Display = new SpeckleDisplayConduit();
      Display.Enabled = true;

      Objects = new List<SpeckleObject>();

      byte[ ] serialisedClient = Convert.FromBase64String( ( string ) info.GetString( "client" ) );

      using ( var ms = new MemoryStream() )
      {
        ms.Write( serialisedClient, 0, serialisedClient.Length );
        ms.Seek( 0, SeekOrigin.Begin );
        Client = ( SpeckleApiClient ) new BinaryFormatter().Deserialize( ms );
        StreamId = Client.StreamId;
      }

      Client.OnReady += Client_OnReady;
      Client.OnLogData += Client_OnLogData;
      Client.OnWsMessage += Client_OnWsMessage;
      Client.OnError += Client_OnError;
    }

    public void GetObjectData( SerializationInfo info, StreamingContext context )
    {
      using ( var ms = new MemoryStream() )
      {
        var formatter = new BinaryFormatter();
        formatter.Serialize( ms, Client );
        info.AddValue( "client", Convert.ToBase64String( ms.ToArray() ) );
        info.AddValue( "paused", Paused );
        info.AddValue( "visible", Visible );
      }
    }
    #endregion
  }
}
