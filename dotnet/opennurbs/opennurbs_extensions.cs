using System;
using System.IO;
using System.Collections.Generic;
using Rhino.Geometry;

namespace Rhino.FileIO
{
  public class File3dm : IDisposable
  {
    IntPtr m_ptr = IntPtr.Zero; //ONX_Model*
    File3dmObjectTable m_object_table;
    File3dmLayerTable m_layer_table;

    internal IntPtr ConstPointer()
    {
      return NonConstPointer(); // all ONX_Models are non-const
    }
    internal IntPtr NonConstPointer()
    {
      if (m_ptr == IntPtr.Zero)
        throw new ObjectDisposedException("File3dm");
      return m_ptr;
    }

    #region statics
    /// <summary>
    /// Read a 3dm file from a specified location
    /// </summary>
    /// <param name="path"></param>
    /// <returns>new File3dm on success, null on error</returns>
    /// <exception cref="FileNotFoundException"></exception>
    public static File3dm Read(string path)
    {
      if (!File.Exists(path))
        throw new FileNotFoundException();
      IntPtr pONX_Model = UnsafeNativeMethods.ONX_Model_ReadFile(path, IntPtr.Zero);
      if (pONX_Model == IntPtr.Zero)
        return null;
      return new File3dm(pONX_Model);
    }

    /// <summary>
    /// Read a 3dm file from a specified location and log any archive
    /// reading errors.
    /// </summary>
    /// <param name="path"></param>
    /// <param name="errorLog">any archive reading errors are logged here</param>
    /// <returns>new File3dm on success, null on error</returns>
    /// <exception cref="FileNotFoundException"></exception>
    public static File3dm ReadWithLog(string path, out string errorLog)
    {
      errorLog = string.Empty;
      if (!File.Exists(path))
        throw new FileNotFoundException();
      using (Rhino.Runtime.StringHolder sh = new Runtime.StringHolder())
      {
        IntPtr pString = sh.NonConstPointer();
        IntPtr pONX_Model = UnsafeNativeMethods.ONX_Model_ReadFile(path, pString);
        errorLog = sh.ToString();
        if (pONX_Model == IntPtr.Zero)
          return null;
        return new File3dm(pONX_Model);
      }
    }
    

    /// <summary>Read only the notes from an existing 3dm file</summary>
    /// <param name="path"></param>
    /// <returns></returns>
    /// <exception cref="FileNotFoundException"></exception>
    public static string ReadNotes(string path)
    {
      if (!File.Exists(path))
        throw new FileNotFoundException();

      using (Rhino.Runtime.StringHolder sh = new Runtime.StringHolder())
      {
        IntPtr pString = sh.NonConstPointer();
        UnsafeNativeMethods.ONX_Model_ReadNotes(path, pString);
        return sh.ToString();
      }
    }
    #endregion

    /// <summary>
    /// Writes contents of this model to an openNURBS archive. I STRONGLY
    /// suggested that you call Polish() before calling Write so that your
    /// file has all the "fluff" that makes it complete.  If the model is
    /// not valid, then Write will refuse to write it.
    /// </summary>
    /// <param name="path"></param>
    /// <param name="version">
    /// Version of the openNURBS archive to write.  Must be 2, 3, 4, or 5.
    /// Rhino 2.x can read version 2 files.
    /// Rhino 3.x can read version 2 and 3 files.
    /// Rhino 4.x can read version 2, 3 and 4 files.
    /// Rhino 5.x can read version 2, 3, 4, and 5 files.
    /// Use version 5 when possible.
    /// </param>
    /// <returns>
    /// True if archive is written with no error.
    /// False if errors occur.
    /// </returns>
    public bool Write(string path, int version)
    {
      IntPtr pThis = NonConstPointer();
      return UnsafeNativeMethods.ONX_Model_WriteFile(pThis, path, version, IntPtr.Zero);
    }

    /// <summary>
    /// Writes contents of this model to an openNURBS archive. I STRONGLY
    /// suggested that you call Polish() before calling Write so that your
    /// file has all the "fluff" that makes it complete.  If the model is
    /// not valid, then Write will refuse to write it.
    /// </summary>
    /// <param name="path">
    /// Version of the openNURBS archive to write.  Must be 2, 3, 4, or 5.
    /// Rhino 2.x can read version 2 files.
    /// Rhino 3.x can read version 2 and 3 files.
    /// Rhino 4.x can read version 2, 3 and 4 files.
    /// Rhino 5.x can read version 2, 3, 4, and 5 files.
    /// Use version 5 when possible.
    /// </param>
    /// <param name="version"></param>
    /// <param name="errorLog"></param>
    /// <returns>
    /// True if archive is written with no error.
    /// False if errors occur.
    /// </returns>
    public bool WriteWithLog(string path, int version, out string errorLog)
    {
      using (Rhino.Runtime.StringHolder sh = new Runtime.StringHolder())
      {
        IntPtr pConstThis = ConstPointer();
        IntPtr pString = sh.NonConstPointer();
        bool rc = UnsafeNativeMethods.ONX_Model_WriteFile(pConstThis, path, version, pString);
        errorLog = sh.ToString();
        return rc;
      }
    }

    /// <summary>
    /// Check a model to make sure it is valid.
    /// </summary>
    /// <param name="errors">
    /// if errors are found, a description of the problem is put in this variable.
    /// </param>
    /// <returns>true if the model is valid</returns>
    public bool IsValid(out string errors)
    {
      IntPtr pConstThis = ConstPointer();
      errors = "";
      using (Rhino.Runtime.StringHolder sh = new Runtime.StringHolder())
      {
        IntPtr pString = sh.NonConstPointer();
        bool rc = UnsafeNativeMethods.ONX_Model_IsValid(pConstThis, pString);
        errors = sh.ToString();
        return rc;
      }
    }

    /// <summary>
    /// Quickly fills in the little details, like making sure there is at least
    /// one layer and table indices make sense.  For a full blown check and repair,
    /// call Audit(true).
    /// </summary>
    public void Polish()
    {
      IntPtr pThis = NonConstPointer();
      UnsafeNativeMethods.ONX_Model_Polish(pThis);
    }

    /// <summary>
    /// Check a model to make sure it is valid and, if possible
    /// and requested, attempt to repair.
    /// </summary>
    /// <param name="attemptRepair">
    /// if true and a problem is found, the problem is repaired
    /// </param>
    /// <param name="repairCount">number of successful repairs</param>
    /// <param name="errors">
    /// if errors are found, a description of the problem is put in this
    /// </param>
    /// <param name="warnings">
    /// If problems were found, warning ids are appended to this list.
    /// 1 (MaterialTable flaws), 2 (LayerTable is not perfect),
    /// 3 (some ObjectTable.Attributes.Id was nil or not unique),
    /// 4 (ObjectTable.IsValid() is false),
    /// 5 (some IDefTable entry has an invalid or duplicate name),
    /// 6 (some IDefTable.ObjectId is not valid),
    /// 7 (some ObjectTable.Geometry is null),
    /// 8 (some ObjectTable.Geometry.IsValid is false),
    /// 9 (some ObjectTable.Attributes is not valid),
    /// 10 (LinetypeTable is not perfect), 11 (LinetypeTable is not perfect),
    /// 12 (some IDefTable.Id was Empty or not unique),
    /// 13 (some TextureMappingTable.MappingId was Empty or not unique),
    /// 14 (some MaterialTable.Id was Empty or not unique),
    /// 15 (some LightTable.LightId was Empty or not unique)
    /// </param>
    /// <returns>
    /// &lt;0 (model has serious errors),
    /// 0 (model is ok),
    /// &gt;0 (number of problems that were found)
    /// </returns>
    public int Audit(bool attemptRepair, out int repairCount, out string errors, out int[] warnings)
    {
      IntPtr pThis = NonConstPointer();
      errors = "";
      repairCount = 0;
      using (Rhino.Runtime.StringHolder sh = new Runtime.StringHolder())
      {
        IntPtr pString = sh.NonConstPointer();
        Rhino.Runtime.InteropWrappers.SimpleArrayInt w = new Runtime.InteropWrappers.SimpleArrayInt();
        IntPtr pWarnings = w.NonConstPointer();
        int rc = UnsafeNativeMethods.ONX_Model_Audit(pThis, attemptRepair, ref repairCount, pString, pWarnings);
        warnings = w.ToArray();
        w.Dispose();
        errors = sh.ToString();
        return rc;
      }
    }

    //int m_3dm_file_version;
    //int m_3dm_opennurbs_version;

    public string StartSectionComments
    {
      get
      {
        IntPtr pConstThis = ConstPointer();
        using (Rhino.Runtime.StringHolder sh = new Runtime.StringHolder())
        {
          IntPtr pString = sh.NonConstPointer();
          UnsafeNativeMethods.ONX_Model_GetStartSectionComments(pConstThis, pString);
          return sh.ToString();
        }
      }
      set
      {
        IntPtr pThis = NonConstPointer();
        UnsafeNativeMethods.ONX_Model_SetStartSectionComments(pThis, value);
      }
    }

    public File3dmNotes Notes
    {
      get
      {
        IntPtr pConstThis = ConstPointer();
        using (Rhino.Runtime.StringHolder sh = new Runtime.StringHolder())
        {
          IntPtr pString = sh.NonConstPointer();
          bool visible = false;
          bool html = false;
          int top=0, bottom=0, left=0, right=0;
          UnsafeNativeMethods.ONX_Model_GetNotes(pConstThis, pString, ref visible, ref html, ref left, ref top, ref right, ref bottom);
          File3dmNotes n = new File3dmNotes();
          n.IsHtml = html;
          n.Notes = sh.ToString();
          n.IsVisible = visible;
          n.WindowRectangle = System.Drawing.Rectangle.FromLTRB(left, top, right, bottom);
          return n;
        }
      }
      set
      {
        IntPtr pThis = NonConstPointer();
        File3dmNotes n = (value!=null)? value : new File3dmNotes();
        UnsafeNativeMethods.ONX_Model_SetNotes(pThis, n.Notes, n.IsVisible, n.IsHtml, n.WindowRectangle.Left, n.WindowRectangle.Top, n.WindowRectangle.Right, n.WindowRectangle.Bottom);
      }
    }

    const int idxApplicationName = 0;
    const int idxApplicationUrl = 1;
    const int idxApplicationDetails = 2;
    const int idxCreatedBy = 3;
    const int idxLastCreatedBy = 4;

    string GetString(int which)
    {
      using (Rhino.Runtime.StringHolder sh = new Runtime.StringHolder())
      {
        IntPtr pConstThis = ConstPointer();
        IntPtr pString = sh.NonConstPointer();
        UnsafeNativeMethods.ONX_Model_GetString(pConstThis, which, pString);
        return sh.ToString();
      }
    }
    void SetString(int which, string val)
    {
      IntPtr pThis = NonConstPointer();
      UnsafeNativeMethods.ONX_Model_SetString(pThis, which, val);
    }

    /// <summary>
    /// Name of the application that wrote this file
    /// </summary>
    public string ApplicationName
    {
      get { return GetString(idxApplicationName); }
      set { SetString(idxApplicationName, value); }
    }
    public string ApplicationUrl
    {
      get { return GetString(idxApplicationUrl); }
      set { SetString(idxApplicationUrl, value); }
    }
    public string ApplicationDetails
    {
      get { return GetString(idxApplicationDetails); }
      set { SetString(idxApplicationDetails, value); }
    }

    public string CreatedBy
    {
      get { return GetString(idxCreatedBy); }
    }
    public string LastEditedBy
    {
      get { return GetString(idxLastCreatedBy); }
    }

    //public DateTime Created { get; }
    //public DateTime LastEdited { get; }

    public int Revision
    {
      get
      {
        IntPtr pConstThis = ConstPointer();
        return UnsafeNativeMethods.ONX_Model_GetRevision(pConstThis);
      }
      set
      {
        IntPtr pThis = NonConstPointer();
        UnsafeNativeMethods.ONX_Model_SetRevision(pThis, value);
      }
    }

    //public System.Drawing.Bitmap PreviewImage { get; set; }

    File3dmSettings m_settings = null;
    /// <summary>
    /// Settings include tolerance, and unit system, and defaults used
    /// for creating views and objects.
    /// </summary>
    public File3dmSettings Settings
    {
      get
      {
        return m_settings ?? (m_settings = new File3dmSettings(this));
      }
    }

    [Obsolete("Use Objects instead. This will be removed from a future WIP")]
    public File3dmObjectTable ObjectTable
    {
      get { return m_object_table ?? (m_object_table = new File3dmObjectTable(this)); }
    }

    public File3dmObjectTable Objects
    {
      get { return m_object_table ?? (m_object_table = new File3dmObjectTable(this)); }
    }

    public IList<Rhino.DocObjects.Layer> Layers
    {
      get { return m_layer_table ?? (m_layer_table = new File3dmLayerTable(this)); }
    }

    #region diagnostic dumps
    const int idxDumpAll = 0;
    const int idxDumpSummary = 1;
    const int idxBitmapTable = 2;
    const int idxTextureMappingTable = 3;
    const int idxMaterialTable = 4;
    const int idxLinetypeTable = 5;
    internal const int idxLayerTable = 6;
    const int idxLightTable = 7;
    const int idxGroupTable = 8;
    const int idxFontTable = 9;
    const int idxDimStyleTable = 10;
    const int idxHatchPatternTable = 11;
    const int idxIDefTable = 12;
    internal const int idxObjectTable = 13;
    const int idxHistoryRecordTable = 14;
    const int idxUserDataTable = 15;
    internal string Dump(int which)
    {
      using (Rhino.Runtime.StringHolder sh = new Runtime.StringHolder())
      {
        IntPtr pConstThis = ConstPointer();
        IntPtr pString = sh.NonConstPointer();
        UnsafeNativeMethods.ONX_Model_Dump(pConstThis, which, pString);
        return sh.ToString();
      }
    }

    /// <summary>Text dump of entire model</summary>
    /// <returns></returns>
    public string Dump()
    {
      return Dump(idxDumpAll);
    }

    /// <summary>text dump of model properties and settings</summary>
    /// <returns></returns>
    public string DumpSummary()
    {
      return Dump(idxDumpSummary);
    }
    /*
    /// <summary>text dump of bitmap table</summary>
    /// <returns></returns>
    public string DumpBitmapTable()
    {
      return Dump(idxDumpBitmapTable);
    }

    /// <summary>text dump of texture mapping table</summary>
    /// <returns></returns>
    public string DumpTextureMappingTable()
    {
      return Dump(idxDumpTextureMappingTable);
    }

    /// <summary>text dump of render material table</summary>
    /// <returns></returns>
    public string DumpMaterialTable()
    {
      return Dump(idxDumpMaterialTable);
    }

    /// <summary>text dump of line type table</summary>
    /// <returns></returns>
    public string DumpLinetypeTable()
    {
      return Dump(idxDumpLinetypeTable);
    }

    /// <summary>text dump of layer table</summary>
    /// <returns></returns>
    public string DumpLayerTable()
    {
      return Dump(idxDumpLayerTable);
    }

    /// <summary>text dump of light table</summary>
    /// <returns></returns>
    public string DumpLightTable()
    {
      return Dump(idxDumpLightTable);
    }

    /// <summary>text dump of group table</summary>
    /// <returns></returns>
    public string DumpGroupTable()
    {
      return Dump(idxDumpGroupTable);
    }

    /// <summary>text dump of font table</summary>
    /// <returns></returns>
    public string DumpFontTable()
    {
      return Dump(idxDumpFontTable);
    }

    /// <summary>text dump of dimstyle table</summary>
    /// <returns></returns>
    public string DumpDimStyleTable()
    {
      return Dump(idxDumpDimStyleTable);
    }

    /// <summary>text dump of hatch pattern table</summary>
    /// <returns></returns>
    public string DumpHatchPatternTable()
    {
      return Dump(idxDumpHatchPatternTable);
    }

    /// <summary>text dump of instance definition table</summary>
    /// <returns></returns>
    public string DumpIDefTable()
    {
      return Dump(idxDumpIDefTable);
    }

    /// <summary>text dump of history record table</summary>
    /// <returns></returns>
    public string DumpHistoryRecordTable()
    {
      return Dump(idxDumpHistoryRecordTable);
    }

    /// <summary>text dump of user data table</summary>
    /// <returns></returns>
    public string DumpUserDataTable()
    {
      return Dump(idxDumpUserDataTable);
    }
    */
    #endregion

    #region constructor-dispose logic
    public File3dm()
    {
      m_ptr = UnsafeNativeMethods.ONX_Model_New();
    }
    private File3dm(IntPtr pONX_Model)
    {
      m_ptr = pONX_Model;
    }

    ~File3dm() { Dispose(false); }
    public void Dispose()
    {
      Dispose(true);
      GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
      if (IntPtr.Zero != m_ptr)
      {
        UnsafeNativeMethods.ONX_Model_Delete(m_ptr);
      }
      m_ptr = IntPtr.Zero;
    }
    #endregion
  }


  /// <summary>
  /// Used to store geometry table object definition and attributes in a File3dm
  /// </summary>
  public class File3dmObject
  {
    int m_index;
    File3dm m_parent;
    Rhino.Geometry.GeometryBase m_geometry;
    Rhino.DocObjects.ObjectAttributes m_attributes;

    internal File3dmObject(int index, File3dm parent)
    {
      m_index = index;
      m_parent = parent;
    }

    internal IntPtr GetGeometryConstPointer()
    {
      IntPtr pModel = m_parent.ConstPointer();
      return UnsafeNativeMethods.ONX_Model_ModelObjectGeometry(pModel, m_index);
    }

    internal IntPtr GetAttributesConstPointer()
    {
      IntPtr pModel = m_parent.ConstPointer();
      return UnsafeNativeMethods.ONX_Model_ModelObjectAttributes(pModel, m_index);
    }

    public Rhino.Geometry.GeometryBase Geometry
    {
      get
      {
        IntPtr pGeometry = GetGeometryConstPointer();
        if( m_geometry==null || m_geometry.ConstPointer()!=pGeometry )
          m_geometry = Rhino.Geometry.GeometryBase.CreateGeometryHelper(pGeometry, this);
        return m_geometry;
      }
    }

    public Rhino.DocObjects.ObjectAttributes Attributes
    {
      get
      {
        IntPtr pAttributes = GetAttributesConstPointer();
        if (m_attributes == null || m_attributes.ConstPointer() != pAttributes)
          m_attributes = new DocObjects.ObjectAttributes(this);
        return m_attributes;
      }
    }
  }

  public class File3dmObjectTable : IEnumerable<File3dmObject>, Rhino.Collections.IRhinoTable<File3dmObject>
  {
    File3dm m_parent;
    internal File3dmObjectTable(File3dm parent)
    {
      m_parent = parent;
    }

    /// <summary>text dump of object table</summary>
    /// <returns></returns>
    public string Dump()
    {
      return m_parent.Dump(File3dm.idxObjectTable);
    }

    #region properties
    /// <summary>
    /// Gets the number of File3dmObjects in this table
    /// </summary>
    public int Count
    {
      get
      {
        IntPtr pConstParent = m_parent.ConstPointer();
        return UnsafeNativeMethods.ONX_Model_TableCount(pConstParent, File3dm.idxObjectTable);
      }
    }

    /// <summary>
    /// Gets the File3dmObject at the given index. 
    /// The index must be valid or an IndexOutOfRangeException will be thrown.
    /// </summary>
    /// <param name="index">Index of File3dmObject to access.</param>
    /// <exception cref="IndexOutOfRangeException">Thrown when the index is invalid.</exception>
    /// <returns>The File3dmObject at [index].</returns>
    public File3dmObject this[int index]
    {
      get
      {
        int count = Count;
        if (index < 0 || index >= count)
          throw new IndexOutOfRangeException();

        if (m_objects == null)
          m_objects = new List<File3dmObject>(count);
        int existing_list_count = m_objects.Count;
        for (int i = existing_list_count; i < count; i++)
        {
          m_objects.Add(new File3dmObject(i, m_parent));
        }

        return m_objects[index];
      }
    }
    List<File3dmObject> m_objects; // = null; initialized to null by runtime
    #endregion

    #region methods

    public File3dmObject[] FindByLayer(string layerName)
    {
      File3dmLayerTable layers = m_parent.Layers as File3dmLayerTable;
      int layer_index = layers.Find(layerName);
      if (layer_index < 0)
        return new File3dmObject[0];

      List<File3dmObject> rc = new List<File3dmObject>();
      int cnt = Count;
      IntPtr pConstModel = m_parent.ConstPointer();
      for (int i = 0; i < cnt; i++)
      {
        if( UnsafeNativeMethods.ONX_Model_ObjectTable_LayerIndexTest(pConstModel, i, layer_index) )
          rc.Add( this[i] );
      }
      return rc.ToArray();
    }


    /// <summary>BoundingBox of every object in this table</summary>
    /// <returns></returns>
    public Rhino.Geometry.BoundingBox GetBoundingBox()
    {
      Rhino.Geometry.BoundingBox bbox = new Geometry.BoundingBox();
      IntPtr pConstModel = m_parent.ConstPointer();
      UnsafeNativeMethods.ONX_Model_BoundingBox(pConstModel, ref bbox);
      return bbox;
    }
    #endregion

    #region IEnumerable Implementation
    public IEnumerator<File3dmObject> GetEnumerator()
    {
      return new Rhino.Collections.TableEnumerator<File3dmObjectTable, File3dmObject>(this);
    }
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
    {
      return new Rhino.Collections.TableEnumerator<File3dmObjectTable, File3dmObject>(this);
    }
    #endregion

    #region Object addition
    /// <summary>
    /// Add a point object to the table.
    /// </summary>
    /// <param name="x">X component of point coordinate.</param>
    /// <param name="y">Y component of point coordinate.</param>
    /// <param name="z">Z component of point coordinate.</param>
    /// <returns>id of new object.</returns>
    public Guid AddPoint(double x, double y, double z)
    {
      return AddPoint(new Point3d(x, y, z));
    }
    /// <summary>Add a point object to the table.</summary>
    /// <param name="point">location of point</param>
    /// <returns>id of new object</returns>
    public Guid AddPoint(Point3d point)
    {
      return AddPoint(point, null);
    }

    /// <summary>Add a point object to the document</summary>
    /// <param name="point">location of point</param>
    /// <param name="attributes">attributes to apply to point</param>
    /// <returns>id of new rhino object</returns>
    public Guid AddPoint(Point3d point, DocObjects.ObjectAttributes attributes)
    {
      IntPtr pConstAttributes = (attributes==null) ? IntPtr.Zero : attributes.ConstPointer();
      IntPtr pThis = m_parent.NonConstPointer();
      m_objects = null; // clear local object list
      return UnsafeNativeMethods.ONX_Model_ObjectTable_AddPoint(pThis, point, pConstAttributes);
    }

    /// <summary>Add a point object to the document.</summary>
    /// <param name="point">location of point</param>
    /// <returns>id of new rhino object</returns>
    public Guid AddPoint(Point3f point)
    {
      Point3d p3d = new Point3d(point);
      return AddPoint(p3d);
    }
    /// <summary>Add a point object to the document</summary>
    /// <param name="point">location of point</param>
    /// <param name="attributes">attributes to apply to point</param>
    /// <returns>id of new rhino object</returns>
    public Guid AddPoint(Point3f point, DocObjects.ObjectAttributes attributes)
    {
      Point3d p3d = new Point3d(point);
      return AddPoint(p3d, attributes);
    }

    /// <summary>
    /// Add multiple points to the document.
    /// </summary>
    /// <param name="points">Points to add.</param>
    /// <returns>List of object ids.</returns>
    public Guid[] AddPoints(IEnumerable<Point3d> points)
    {
      if (points == null) { throw new ArgumentNullException("points"); }

      List<Guid> ids = new List<Guid>();
      foreach (Point3d pt in points)
      {
        ids.Add(AddPoint(pt));
      }

      return ids.ToArray();
    }
    /// <summary>
    /// Add multiple points to the document.
    /// </summary>
    /// <param name="points">Points to add.</param>
    /// <param name="attributes">Attributes to apply to point objects.</param>
    /// <returns>List of object ids.</returns>
    public Guid[] AddPoints(IEnumerable<Point3d> points, DocObjects.ObjectAttributes attributes)
    {
      if (points == null) { throw new ArgumentNullException("points"); }

      List<Guid> ids = new List<Guid>();
      foreach (Point3d pt in points)
      {
        ids.Add(AddPoint(pt, attributes));
      }

      return ids.ToArray();
    }

    /// <summary>
    /// Add multiple points to the document.
    /// </summary>
    /// <param name="points">Points to add.</param>
    /// <returns>List of object ids.</returns>
    public Guid[] AddPoints(IEnumerable<Point3f> points)
    {
      if (points == null) { throw new ArgumentNullException("points"); }

      List<Guid> ids = new List<Guid>();
      foreach (Point3f pt in points)
      {
        ids.Add(AddPoint(pt));
      }

      return ids.ToArray();
    }
    /// <summary>
    /// Add multiple points to the document.
    /// </summary>
    /// <param name="points">Points to add.</param>
    /// <param name="attributes">Attributes to apply to point objects.</param>
    /// <returns>List of object ids.</returns>
    public Guid[] AddPoints(IEnumerable<Point3f> points, DocObjects.ObjectAttributes attributes)
    {
      if (points == null) { throw new ArgumentNullException("points"); }

      List<Guid> ids = new List<Guid>();
      foreach (Point3f pt in points)
      {
        ids.Add(AddPoint(pt, attributes));
      }

      return ids.ToArray();
    }

    /// <summary>Add a point cloud object to the document</summary>
    /// <param name="cloud">PointCloud to add.</param>
    /// <returns>id of new rhino object</returns>
    public Guid AddPointCloud(PointCloud cloud)
    {
      return AddPointCloud(cloud, null);
    }
    /// <summary>Add a point cloud object to the document</summary>
    /// <param name="cloud">PointCloud to add.</param>
    /// <param name="attributes">attributes to apply to point cloud</param>
    /// <returns>id of new rhino object</returns>
    public Guid AddPointCloud(PointCloud cloud, DocObjects.ObjectAttributes attributes)
    {
      if (cloud == null) { throw new ArgumentNullException("cloud"); }

      IntPtr pCloud = cloud.ConstPointer();

      IntPtr pThis = m_parent.NonConstPointer();

      IntPtr pAttrs = IntPtr.Zero;
      if (null != attributes)
        pAttrs = attributes.ConstPointer();
      m_objects = null;
      return UnsafeNativeMethods.ONX_Model_ObjectTable_AddPointCloud2(pThis, pCloud, pAttrs);
    }
    /// <summary>Add a point cloud object to the document</summary>
    /// <param name="points"></param>
    /// <returns>id of new rhino object</returns>
    public Guid AddPointCloud(IEnumerable<Point3d> points)
    {
      return AddPointCloud(points, null);
    }
    /// <summary>Add a point cloud object to the document</summary>
    /// <param name="points"></param>
    /// <param name="attributes">attributes to apply to point cloud</param>
    /// <returns>id of new rhino object</returns>
    public Guid AddPointCloud(IEnumerable<Point3d> points, DocObjects.ObjectAttributes attributes)
    {
      int count;
      Point3d[] ptArray = Collections.Point3dList.GetConstPointArray(points, out count);
      if (null == ptArray || count < 1)
        return Guid.Empty;

      IntPtr pThis = m_parent.NonConstPointer();

      IntPtr pAttrs = IntPtr.Zero;
      if (null != attributes)
        pAttrs = attributes.ConstPointer();
      m_objects = null;
      return UnsafeNativeMethods.ONX_Model_ObjectTable_AddPointCloud(pThis, count, ptArray, pAttrs);
    }

    /// <summary>
    /// Add a clipping plane object to Rhino
    /// </summary>
    /// <param name="plane"></param>
    /// <param name="uMagnitude"></param>
    /// <param name="vMagnitude"></param>
    /// <param name="clippedViewportId">viewport id that the new clipping plane will clip</param>
    /// <returns>id of new rhino object</returns>
    /// <example>
    /// <code source='examples\vbnet\ex_addclippingplane.vb' lang='vbnet'/>
    /// <code source='examples\cs\ex_addclippingplane.cs' lang='cs'/>
    /// <code source='examples\py\ex_addclippingplane.py' lang='py'/>
    /// </example>
    public Guid AddClippingPlane(Plane plane, double uMagnitude, double vMagnitude, Guid clippedViewportId)
    {
      return AddClippingPlane(plane, uMagnitude, vMagnitude, new Guid[] { clippedViewportId });
    }
    /// <summary>
    /// Add a clipping plane object to Rhino
    /// </summary>
    /// <param name="plane"></param>
    /// <param name="uMagnitude"></param>
    /// <param name="vMagnitude"></param>
    /// <param name="clippedViewportIds">list of viewport ids that the new clipping plane will clip</param>
    /// <returns>id of new rhino object</returns>
    public Guid AddClippingPlane(Plane plane, double uMagnitude, double vMagnitude, IEnumerable<Guid> clippedViewportIds)
    {
      return AddClippingPlane(plane, uMagnitude, vMagnitude, clippedViewportIds, null);
    }
    /// <summary>
    /// Add a clipping plane object to Rhino
    /// </summary>
    /// <param name="plane"></param>
    /// <param name="uMagnitude"></param>
    /// <param name="vMagnitude"></param>
    /// <param name="clippedViewportIds">list of viewport ids that the new clipping plane will clip</param>
    /// <param name="attributes"></param>
    /// <returns>id of new rhino object</returns>
    public Guid AddClippingPlane(Plane plane, double uMagnitude, double vMagnitude, IEnumerable<Guid> clippedViewportIds, DocObjects.ObjectAttributes attributes)
    {
      IntPtr pAttrs = (null == attributes) ? IntPtr.Zero : attributes.ConstPointer();
      List<Guid> ids = new List<Guid>();
      foreach (Guid item in clippedViewportIds)
        ids.Add(item);
      Guid[] clippedIds = ids.ToArray();
      int count = clippedIds.Length;
      if (count < 1)
        return Guid.Empty;
      IntPtr pThis = m_parent.NonConstPointer();
      m_objects = null;
      Guid rc = UnsafeNativeMethods.ONX_Model_ObjectTable_AddClippingPlane(pThis, ref plane, uMagnitude, vMagnitude, count, clippedIds, pAttrs);
      return rc;
    }

    public Guid AddLinearDimension(LinearDimension dimension)
    {
      return AddLinearDimension(dimension, null);
    }

    public Guid AddLinearDimension(LinearDimension dimension, DocObjects.ObjectAttributes attributes)
    {
      IntPtr pConstDimension = dimension.ConstPointer();
      IntPtr pAttributes = (attributes==null)?IntPtr.Zero : attributes.ConstPointer();
      IntPtr pThis = m_parent.NonConstPointer();
      m_objects = null;
      return UnsafeNativeMethods.ONX_Model_ObjectTable_AddLinearDimension(pThis, pConstDimension, pAttributes);
    }

    /// <summary>Add a line object to Rhino</summary>
    /// <param name="from"></param>
    /// <param name="to"></param>
    /// <returns>id of new rhino object</returns>
    public Guid AddLine(Point3d from, Point3d to)
    {
      return AddLine(from, to, null);
    }
    /// <summary>Add a line object to Rhino</summary>
    /// <param name="from"></param>
    /// <param name="to"></param>
    /// <param name="attributes">attributes to apply to line</param>
    /// <returns>id of new rhino object</returns>
    public Guid AddLine(Point3d from, Point3d to, DocObjects.ObjectAttributes attributes)
    {
      IntPtr pAttr = (null == attributes) ? IntPtr.Zero : attributes.ConstPointer();
      IntPtr pThis = m_parent.NonConstPointer();
      m_objects = null;
      return UnsafeNativeMethods.ONX_Model_ObjectTable_AddLine(pThis, from, to, pAttr);
    }
    /// <summary>Add a line object to Rhino</summary>
    /// <returns>id of new rhino object</returns>
    public Guid AddLine(Line line)
    {
      return AddLine(line.From, line.To);
    }
    /// <summary>Add a line object to Rhino</summary>
    /// <param name="line"></param>
    /// <param name="attributes">attributes to apply to line</param>
    /// <returns>id of new rhino object</returns>
    public Guid AddLine(Line line, DocObjects.ObjectAttributes attributes)
    {
      return AddLine(line.From, line.To, attributes);
    }

    /// <summary>Add a polyline object to Rhino</summary>
    /// <param name="points"></param>
    /// <returns>id of new rhino object</returns>
    public Guid AddPolyline(IEnumerable<Point3d> points)
    {
      return AddPolyline(points, null);
    }
    /// <summary>Add a polyline object to Rhino</summary>
    /// <param name="points"></param>
    /// <param name="attributes">attributes to apply to line</param>
    /// <returns>id of new rhino object</returns>
    public Guid AddPolyline(IEnumerable<Point3d> points, DocObjects.ObjectAttributes attributes)
    {
      int count;
      Point3d[] ptArray = Collections.Point3dList.GetConstPointArray(points, out count);
      if (null == ptArray || count < 1)
        return Guid.Empty;

      IntPtr pAttrs = (attributes == null) ? IntPtr.Zero : attributes.ConstPointer();
      IntPtr pThis = m_parent.NonConstPointer();
      m_objects = null;
      return UnsafeNativeMethods.ONX_Model_ObjectTable_AddPolyLine(pThis, count, ptArray, pAttrs);
    }

    /// <summary>Add a curve object to the document representing an arc</summary>
    /// <param name="arc"></param>
    /// <returns>id of new rhino object</returns>
    public Guid AddArc(Arc arc)
    {
      return AddArc(arc, null);
    }
    /// <summary>Add a curve object to the document representing an arc</summary>
    /// <param name="arc"></param>
    /// <param name="attributes">attributes to apply to arc</param>
    /// <returns>id of new rhino object</returns>
    public Guid AddArc(Arc arc, DocObjects.ObjectAttributes attributes)
    {
      IntPtr pAttr = (attributes == null) ? IntPtr.Zero : attributes.ConstPointer();
      IntPtr pThis = m_parent.NonConstPointer();
      m_objects = null;
      return UnsafeNativeMethods.ONX_Model_ObjectTable_AddArc(pThis, ref arc, pAttr);
    }

    /// <summary>Add a curve object to the document representing a circle</summary>
    /// <param name="circle"></param>
    /// <returns>id of new rhino object</returns>
    public Guid AddCircle(Circle circle)
    {
      return AddCircle(circle, null);
    }
    /// <summary>Add a curve object to the document representing a circle</summary>
    /// <param name="circle"></param>
    /// <param name="attributes">attributes to apply to circle</param>
    /// <returns>id of new rhino object</returns>
    public Guid AddCircle(Circle circle, DocObjects.ObjectAttributes attributes)
    {
      IntPtr pAttr = (attributes == null) ? IntPtr.Zero : attributes.ConstPointer();
      IntPtr pThis = m_parent.NonConstPointer();
      m_objects = null;
      return UnsafeNativeMethods.ONX_Model_ObjectTable_AddCircle(pThis, ref circle, pAttr);
    }

    /// <summary>Add a curve object to the document representing an ellipse</summary>
    /// <param name="ellipse"></param>
    /// <returns>id of new rhino object</returns>
    public Guid AddEllipse(Ellipse ellipse)
    {
      return AddEllipse(ellipse, null);
    }
    /// <summary>Add a curve object to the document representing an ellipse</summary>
    /// <param name="ellipse"></param>
    /// <param name="attributes">attributes to apply to ellipse</param>
    /// <returns>id of new rhino object</returns>
    public Guid AddEllipse(Ellipse ellipse, DocObjects.ObjectAttributes attributes)
    {
      IntPtr pAttr = (attributes == null) ? IntPtr.Zero : attributes.ConstPointer();
      IntPtr pThis = m_parent.NonConstPointer();
      m_objects = null;
      return UnsafeNativeMethods.ONX_Model_ObjectTable_AddEllipse(pThis, ref ellipse, pAttr);
    }

    public Guid AddSphere(Sphere sphere)
    {
      return AddSphere(sphere, null);
    }
    public Guid AddSphere(Sphere sphere, DocObjects.ObjectAttributes attributes)
    {
      IntPtr pAttr = (attributes == null) ? IntPtr.Zero : attributes.ConstPointer();
      IntPtr pThis = m_parent.NonConstPointer();
      m_objects = null;
      return UnsafeNativeMethods.ONX_Model_ObjectTable_AddSphere(pThis, ref sphere, pAttr);
    }

    /// <summary>Add a curve object to Rhino</summary>
    /// <param name="curve"></param>
    /// <returns>id of new rhino object</returns>
    public Guid AddCurve(Geometry.Curve curve)
    {
      return AddCurve(curve, null);
    }
    /// <summary>Add a curve object to Rhino</summary>
    /// <param name="curve">A duplicate of this curve is added to Rhino</param>
    /// <param name="attributes">attributes to apply to curve</param>
    /// <returns>id of new rhino object</returns>
    public Guid AddCurve(Geometry.Curve curve, DocObjects.ObjectAttributes attributes)
    {
      IntPtr pAttr = (attributes == null) ? IntPtr.Zero : attributes.ConstPointer();
      IntPtr pThis = m_parent.NonConstPointer();
      IntPtr curvePtr = curve.ConstPointer();
      m_objects = null;
      return UnsafeNativeMethods.ONX_Model_ObjectTable_AddCurve(pThis, curvePtr, pAttr);
    }

    /// <summary>Add a text dot object to Rhino</summary>
    /// <param name="text"></param>
    /// <param name="location"></param>
    /// <returns>id of new rhino object</returns>
    public Guid AddTextDot(string text, Point3d location)
    {
      Geometry.TextDot dot = new Rhino.Geometry.TextDot(text, location);
      Guid rc = AddTextDot(dot);
      dot.Dispose();
      return rc;
    }
    /// <summary>Add a text dot object to Rhino</summary>
    /// <param name="text"></param>
    /// <param name="location"></param>
    /// <param name="attributes"></param>
    /// <returns>id of new rhino object</returns>
    public Guid AddTextDot(string text, Point3d location, DocObjects.ObjectAttributes attributes)
    {
      Geometry.TextDot dot = new Rhino.Geometry.TextDot(text, location);
      Guid rc = AddTextDot(dot, attributes);
      dot.Dispose();
      return rc;
    }
    /// <summary>Add a text dot object to Rhino</summary>
    /// <param name="dot"></param>
    /// <returns>id of new rhino object</returns>
    public Guid AddTextDot(Geometry.TextDot dot)
    {
      return AddTextDot(dot, null);
    }
    /// <summary>Add a text dot object to Rhino</summary>
    /// <param name="dot"></param>
    /// <param name="attributes"></param>
    /// <returns>id of new rhino object</returns>
    public Guid AddTextDot(Geometry.TextDot dot, DocObjects.ObjectAttributes attributes)
    {
      IntPtr pAttr = (attributes == null) ? IntPtr.Zero : attributes.ConstPointer();
      IntPtr pThis = m_parent.NonConstPointer();
      IntPtr pDot = dot.ConstPointer();
      m_objects = null;
      return UnsafeNativeMethods.ONX_Model_ObjectTable_AddTextDot(pThis, pDot, pAttr);
    }

    /// <summary>
    /// Add an annotation text object to the document.
    /// </summary>
    /// <param name="text3d">The text object to add.</param>
    /// <returns>The Guid of the newly added object or Guid.Empty on failure.</returns>
    public Guid AddText(Rhino.Display.Text3d text3d)
    {
      return AddText(text3d.Text, text3d.TextPlane, text3d.Height, text3d.FontFace, text3d.Bold, text3d.Italic);
    }
    /// <summary>
    /// Add an annotation text object to the document.
    /// </summary>
    /// <param name="text3d">The text object to add.</param>
    /// <param name="attributes">Object Attributes.</param>
    /// <returns>The Guid of the newly added object or Guid.Empty on failure.</returns>
    public Guid AddText(Rhino.Display.Text3d text3d, DocObjects.ObjectAttributes attributes)
    {
      return AddText(text3d.Text, text3d.TextPlane, text3d.Height, text3d.FontFace, text3d.Bold, text3d.Italic, attributes);
    }
    /// <summary>
    /// Add an annotation text object to the document.
    /// </summary>
    /// <param name="text">Text string.</param>
    /// <param name="plane">Plane of text.</param>
    /// <param name="height">Height of text.</param>
    /// <param name="fontName">Name of FontFace.</param>
    /// <param name="bold">Bold flag.</param>
    /// <param name="italic">Italic flag.</param>
    /// <returns>The Guid of the newly added object or Guid.Empty on failure.</returns>
    public Guid AddText(string text, Plane plane, double height, string fontName, bool bold, bool italic)
    {
      return AddText(text, plane, height, fontName, bold, italic, null);
    }

    public Guid AddText(string text, Plane plane, double height, string fontName, bool bold, bool italic, TextJustification justification)
    {
      return AddText(text, plane, height, fontName, bold, italic, justification, null);
    }

    public Guid AddText(string text, Plane plane, double height, string fontName, bool bold, bool italic, TextJustification justification, DocObjects.ObjectAttributes attributes)
    {
      if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(fontName))
        return Guid.Empty;
      IntPtr pAttr = (attributes == null) ? IntPtr.Zero : attributes.ConstPointer();
      IntPtr pThis = m_parent.NonConstPointer();
      int fontStyle = 0;
      if (bold)
        fontStyle |= 1;
      if (italic)
        fontStyle |= 2;
      m_objects = null;
      Guid rc = UnsafeNativeMethods.ONX_Model_ObjectTable_AddText(pThis, text, ref plane, height, fontName, fontStyle, (int)justification, pAttr);
      return rc;
    }

    /// <summary>
    /// Add an annotation text object to the document.
    /// </summary>
    /// <param name="text">Text string.</param>
    /// <param name="plane">Plane of text.</param>
    /// <param name="height">Height of text.</param>
    /// <param name="fontName">Name of FontFace.</param>
    /// <param name="bold">Bold flag.</param>
    /// <param name="italic">Italic flag.</param>
    /// <param name="attributes">Object Attributes.</param>
    /// <returns>The Guid of the newly added object or Guid.Empty on failure.</returns>
    public Guid AddText(string text, Plane plane, double height, string fontName, bool bold, bool italic, DocObjects.ObjectAttributes attributes)
    {
      return AddText(text, plane, height, fontName, bold, italic, TextJustification.None, attributes);
    }

    /// <summary>Add a surface object to Rhino</summary>
    /// <param name="surface">A duplicate of this surface is added to Rhino</param>
    /// <returns>id of new rhino object</returns>
    public Guid AddSurface(Geometry.Surface surface)
    {
      return AddSurface(surface, null);
    }
    /// <summary>Add a surface object to Rhino</summary>
    /// <param name="surface">A duplicate of this surface is added to Rhino</param>
    /// <param name="attributes"></param>
    /// <returns>id of new rhino object</returns>
    public Guid AddSurface(Geometry.Surface surface, DocObjects.ObjectAttributes attributes)
    {
      IntPtr pAttr = (attributes == null) ? IntPtr.Zero : attributes.ConstPointer();
      IntPtr pThis = m_parent.NonConstPointer();
      IntPtr pSurface = surface.ConstPointer();
      m_objects = null;
      return UnsafeNativeMethods.ONX_Model_ObjectTable_AddSurface(pThis, pSurface, pAttr);
    }

#if USING_V5_SDK
    /// <summary>Add an extrusion object to Rhino</summary>
    /// <param name="extrusion">A duplicate of this extrusion is added to Rhino</param>
    /// <returns>id of new rhino object</returns>
    public Guid AddExtrusion(Geometry.Extrusion extrusion)
    {
      return AddExtrusion(extrusion, null);
    }
    /// <summary>Add an extrusion object to Rhino</summary>
    /// <param name="extrusion">A duplicate of this extrusion is added to Rhino</param>
    /// <param name="attributes"></param>
    /// <returns>id of new rhino object</returns>
    public Guid AddExtrusion(Geometry.Extrusion extrusion, DocObjects.ObjectAttributes attributes)
    {
      IntPtr pAttr = (attributes == null) ? IntPtr.Zero : attributes.ConstPointer();
      IntPtr pThis = m_parent.NonConstPointer();
      IntPtr pConstExtrusion = extrusion.ConstPointer();
      m_objects = null;
      return UnsafeNativeMethods.ONX_Model_ObjectTable_AddExtrusion(pThis, pConstExtrusion, pAttr);
    }
#endif

    /// <summary>Add a mesh object to Rhino</summary>
    /// <param name="mesh">A duplicate of this mesh is added to Rhino</param>
    /// <returns>id of new rhino object</returns>
    public Guid AddMesh(Geometry.Mesh mesh)
    {
      return AddMesh(mesh, null);
    }
    /// <summary>Add a mesh object to Rhino</summary>
    /// <param name="mesh">A duplicate of this mesh is added to Rhino</param>
    /// <param name="attributes"></param>
    /// <returns>id of new rhino object</returns>
    public Guid AddMesh(Geometry.Mesh mesh, DocObjects.ObjectAttributes attributes)
    {
      IntPtr pAttr = (attributes == null) ? IntPtr.Zero : attributes.ConstPointer();
      IntPtr pThis = m_parent.NonConstPointer();
      IntPtr pConstMesh = mesh.ConstPointer();
      m_objects = null;
      return UnsafeNativeMethods.ONX_Model_ObjectTable_AddMesh(pThis, pConstMesh, pAttr);
    }

    /// <summary>Add a brep object to Rhino</summary>
    /// <param name="brep">A duplicate of this brep is added to Rhino</param>
    /// <returns>id of new rhino object</returns>
    public Guid AddBrep(Geometry.Brep brep)
    {
      return AddBrep(brep, null);
    }
    /// <summary>Add a brep object to Rhino</summary>
    /// <param name="brep">A duplicate of this brep is added to Rhino</param>
    /// <param name="attributes">attributes to apply to brep</param>
    /// <returns>id of new rhino object</returns>
    public Guid AddBrep(Geometry.Brep brep, DocObjects.ObjectAttributes attributes)
    {
      IntPtr pAttr = (attributes == null) ? IntPtr.Zero : attributes.ConstPointer();
      IntPtr pThis = m_parent.NonConstPointer();
      IntPtr pConstBrep = brep.ConstPointer();
      m_objects = null;
      return UnsafeNativeMethods.ONX_Model_ObjectTable_AddBrep(pThis, pConstBrep, pAttr);
    }

    /*
     * //not yet
    public Guid AddInstanceObject(int instanceDefinitionIndex, Transform instanceXform)
    {
      return AddInstanceObject(instanceDefinitionIndex, instanceXform, null);
    }

    public Guid AddInstanceObject(int instanceDefinitionIndex, Transform instanceXform, DocObjects.ObjectAttributes attributes)
    {
      IntPtr pAttr = (attributes == null) ? IntPtr.Zero : attributes.ConstPointer();
      IntPtr pThis = m_parent.NonConstPointer();
      IntPtr pConstBrep = brep.ConstPointer();
      return UnsafeNativeMethods.ONX_Model_ObjectTable_AddInstanceObject(m_doc.m_docId, instanceDefinitionIndex, ref instanceXform, pAttributes);
    }
    */

    public Guid AddLeader(Plane plane, IEnumerable<Point2d> points)
    {
      return AddLeader(null, plane, points);
    }
    public Guid AddLeader(Plane plane, IEnumerable<Point2d> points, DocObjects.ObjectAttributes attributes)
    {
      return AddLeader(null, plane, points, attributes);
    }

    public Guid AddLeader(string text, Plane plane, IEnumerable<Point2d> points, DocObjects.ObjectAttributes attributes)
    {
      string s = null;
      if (!string.IsNullOrEmpty(text))
        s = text;
      Rhino.Collections.RhinoList<Point2d> pts = new Rhino.Collections.RhinoList<Point2d>();
      foreach (Point2d pt in points)
        pts.Add(pt);
      int count = pts.Count;
      if (count < 1)
        return Guid.Empty;

      IntPtr pAttr = (attributes == null) ? IntPtr.Zero : attributes.ConstPointer();
      IntPtr pThis = m_parent.NonConstPointer();

      return UnsafeNativeMethods.ONX_Model_ObjectTable_AddLeader(pThis, s, ref plane, count, pts.m_items, pAttr);
    }

    public Guid AddLeader(string text, Plane plane, IEnumerable<Point2d> points)
    {
      return AddLeader(text, plane, points, null);
    }

#if RHINO_SDK
    public Guid AddLeader(string text, IEnumerable<Point3d> points)
    {
      Plane plane;
      //double max_deviation;
      PlaneFitResult rc = Plane.FitPlaneToPoints(points, out plane);//, out max_deviation);
      if (rc != PlaneFitResult.Success)
        return Guid.Empty;

      Rhino.Collections.RhinoList<Point2d> points2d = new Rhino.Collections.RhinoList<Point2d>();
      foreach (Point3d point3d in points)
      {
        double s, t;
        if (plane.ClosestParameter(point3d, out s, out t))
        {
          Point2d newpoint = new Point2d(s, t);
          if (points2d.Count > 0 && points2d.Last.DistanceTo(newpoint) < Rhino.RhinoMath.SqrtEpsilon)
            continue;
          points2d.Add(new Point2d(s, t));
        }
      }
      return AddLeader(text, plane, points2d);
    }

    public Guid AddLeader(IEnumerable<Point3d> points)
    {
      return AddLeader(null, points);
    }
#endif

    public Guid AddHatch(Hatch hatch)
    {
      return AddHatch(hatch, null);
    }
    public Guid AddHatch(Hatch hatch, DocObjects.ObjectAttributes attributes)
    {
      IntPtr pConstHatch = hatch.ConstPointer();
      IntPtr pAttr = (attributes == null) ? IntPtr.Zero : attributes.ConstPointer();
      IntPtr pThis = m_parent.NonConstPointer();
      return UnsafeNativeMethods.ONX_Model_ObjectTable_AddHatch(pThis, pConstHatch, pAttr);
    }
    #endregion
  }

  class File3dmLayerTable : IList<Rhino.DocObjects.Layer>, Rhino.Collections.IRhinoTable<Rhino.DocObjects.Layer>
  {
    File3dm m_parent;
    internal File3dmLayerTable(File3dm parent)
    {
      m_parent = parent;
    }

    public int Find(string name)
    {
      int cnt = Count;
      for (int i = 0; i < cnt; i++)
      {
        if (this[i].Name.Equals(name, StringComparison.OrdinalIgnoreCase))
          return i;
      }
      return -1;
    }

    public int IndexOf(DocObjects.Layer item)
    {
      File3dm file = item.m__parent as File3dm;
      if (file == m_parent)
        return item.LayerIndex;
      return -1;
    }

    public void Insert(int index, DocObjects.Layer item)
    {
      if (index < 0 || index > Count)
        throw new IndexOutOfRangeException();
      IntPtr pParent = m_parent.NonConstPointer();
      IntPtr pConstLayer = item.ConstPointer();
      UnsafeNativeMethods.ONX_Model_LayerTable_Insert(pParent, pConstLayer, index);
    }

    public void RemoveAt(int index)
    {
      if (index < 0 || index >= Count)
        throw new IndexOutOfRangeException();
      IntPtr pParent = m_parent.NonConstPointer();
      UnsafeNativeMethods.ONX_Model_LayerTable_RemoveAt(pParent, index);
    }

    public DocObjects.Layer this[int index]
    {
      get
      {
        IntPtr pConstParent = m_parent.ConstPointer();
        Guid id = UnsafeNativeMethods.ONX_Model_LayerTable_Id(pConstParent, index);
        if (id==Guid.Empty )
          throw new IndexOutOfRangeException();
        return new DocObjects.Layer(id, m_parent);
      }
      set
      {
        Insert(index, value);
      }
    }

    public void Add(DocObjects.Layer item)
    {
      int index = Count;
      Insert(index, item);
    }

    public void Clear()
    {
      IntPtr pParent = m_parent.NonConstPointer();
      UnsafeNativeMethods.ONX_Model_LayerTable_Clear(pParent);
    }

    public bool Contains(DocObjects.Layer item)
    {
      return IndexOf(item) != -1;
    }

    public void CopyTo(DocObjects.Layer[] array, int arrayIndex)
    {
      int available = array.Length - arrayIndex;
      int cnt = Count;
      if (available < cnt)
        throw new ArgumentException("The number of elements in the source ICollection<T> is greater than the available space from arrayIndex to the end of the destination array.");
      for (int i = 0; i < cnt; i++)
      {
        array[arrayIndex++] = this[i];
      }
    }

    public int Count
    {
      get
      {
        IntPtr pConstParent = m_parent.ConstPointer();
        return UnsafeNativeMethods.ONX_Model_TableCount(pConstParent, File3dm.idxLayerTable);
      }
    }

    public bool IsReadOnly
    {
      get { return false; }
    }

    public bool Remove(DocObjects.Layer item)
    {
      int index = IndexOf(item);
      if (index >= 0)
        RemoveAt(index);
      return (index >= 0);
    }

    public IEnumerator<DocObjects.Layer> GetEnumerator()
    {
      return new Rhino.Collections.TableEnumerator<File3dmLayerTable, Rhino.DocObjects.Layer>(this);
    }

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
    {
      return new Rhino.Collections.TableEnumerator<File3dmLayerTable, Rhino.DocObjects.Layer>(this);
    }
  }
}