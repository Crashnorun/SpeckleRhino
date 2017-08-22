﻿using System;
using System.Collections.Generic;
using System.Linq;

using Grasshopper.Kernel;
using Rhino.Geometry;

using GH_IO.Serialization;
using System.Diagnostics;
using Grasshopper.Kernel.Parameters;

using SpeckleGhRhConverter;


using Grasshopper;
using Grasshopper.Kernel.Data;

using Newtonsoft.Json;
using System.Dynamic;

using SpeckleCore;

namespace SpeckleGrasshopper
{
    public class EncodeToSpeckle : GH_Component
    {

        Converter c = new RhinoConverter();

        public EncodeToSpeckle()
          : base("Speckle Converter", "Speckle Converter",
              "Speckle Converter",
              "Speckle", "Debug")
        {
        }

        public override Guid ComponentGuid
        {
            get { return new Guid("{c4442de1-c440-40ba-8da7-33c89eb1a529}"); }
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Object", "O", "Objects to convert.", GH_ParamAccess.item);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Conversion Result String", "S", "Conversion result string.", GH_ParamAccess.item);
            pManager.AddGenericParameter("Conversion Result", "R", "Conversion result object.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            object myObj = new object();
            DA.GetData(0, ref myObj);

            var result = c.ToSpeckle(myObj);
            DA.SetData(0, JsonConvert.SerializeObject(result, Formatting.Indented));
            DA.SetData(1, result);
        }

        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                //You can add image files to your project resources and access them like this:
                // return Resources.IconForThisComponent;
                return null;
            }
        }
    }

    public class DecodeFromSpeckle : GH_Component
    {

        Converter c = new RhinoConverter();

        public DecodeFromSpeckle()
          : base("Speckle Decoder", "Speckle Decoder",
              "Speckle Decoder",
              "Speckle", "Debug")
        {
        }

        public override Guid ComponentGuid
        {
            get { return new Guid("{43b4f541-d914-471e-9f37-72291db2f2d4}"); }
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Object", "O", "Objects to cast.", GH_ParamAccess.item);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Conversion Result", "R", "Conversion result object.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            object myObj = new object();
            DA.GetData(0, ref myObj);

            var cast = myObj as Grasshopper.Kernel.Types.GH_ObjectWrapper;

            var result = c.ToNative((SpeckleObject) cast.Value);
            DA.SetData(0, result);
        }

        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                //You can add image files to your project resources and access them like this:
                // return Resources.IconForThisComponent;
                return null;
            }
        }
    }
}
