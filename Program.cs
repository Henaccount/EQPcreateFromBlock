using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Autodesk.ProcessPower.DataLinks;
using Autodesk.ProcessPower.DataObjects;
using Autodesk.ProcessPower.PartsRepository;
using Autodesk.ProcessPower.PnP3dDataLinks;
using Autodesk.ProcessPower.PnP3dEquipment;
using Autodesk.ProcessPower.PnP3dObjects;
using Autodesk.ProcessPower.ProjectManager;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using PlantApp = Autodesk.ProcessPower.PlantInstance.PlantApplication;

namespace EQPcreateFromBlock
{
    public class Program
    {
        [CommandMethod("EQPcreateFromBlock")]
        public void EQPcreateFromBlock()
        {
            //hardcoded:
            string eqClass = "Vessel";

            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            Editor ed = doc.Editor;

            // Prompt user to select a block reference
            PromptEntityOptions peo = new PromptEntityOptions("\nSelect a block reference: ");
            peo.SetRejectMessage("Selected entity must be a block reference.");
            peo.AddAllowedClass(typeof(BlockReference), true);
            PromptEntityResult per = ed.GetEntity(peo);

            if (per.Status != PromptStatus.OK)
            {
                ed.WriteMessage("\nNo valid block reference selected.");
                return;
            }

            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                BlockReference blockRef = tr.GetObject(per.ObjectId, OpenMode.ForRead) as BlockReference;
                if (blockRef != null)
                {
                    string blockName = blockRef.Name;

                    //ed.WriteMessage($"\nSelected block name: {blockName}");

                    Point3d blockbasePoint = blockRef.Position;

                    ObjectId blockId = per.ObjectId;

                    Matrix3d blockTransf = blockRef.BlockTransform;
                    double[] dMatrix = new double[16];
                    dMatrix[0] = 1.0;
                    dMatrix[1] = 0.0;
                    dMatrix[2] = 0.0;
                    dMatrix[3] = 0.0;
                    dMatrix[4] = 0.0;
                    dMatrix[5] = 1.0;
                    dMatrix[6] = 0.0;
                    dMatrix[7] = 0.0;
                    dMatrix[8] = 0.0;
                    dMatrix[9] = 0.0;
                    dMatrix[10] = 1.0;
                    dMatrix[11] = 0.0;
                    dMatrix[12] = 0.0;
                    dMatrix[13] = 0.0;
                    dMatrix[14] = 0.0;
                    dMatrix[15] = 1.0;
                    Matrix3d nullMatrix = new Matrix3d(dMatrix);

                    Matrix3d subblockTransform = new Matrix3d();
                    //ed.WriteMessage("\nnullMatrix: " + nullMatrix.ToString());

                    List<object[]> blockInfoList = new List<object[]>();

                    BlockTableRecord btr = (BlockTableRecord)tr.GetObject(blockRef.BlockTableRecord, OpenMode.ForRead);

                    BlockTableRecordEnumerator Enum = btr.GetEnumerator();

                    while (Enum.MoveNext())
                    {
                        Entity ent = tr.GetObject(Enum.Current, OpenMode.ForRead) as Entity;

                        //ed.WriteMessage("\ntype: " + ent.GetType());

                        if (Enum.Current.ObjectClass.Name.Equals("AcDbBlockReference"))
                        {
                            BlockReference subblockref = (BlockReference)tr.GetObject(Enum.Current, OpenMode.ForRead);
                            BlockTableRecord subblockbtr = (BlockTableRecord)tr.GetObject(subblockref.BlockTableRecord, OpenMode.ForRead);
                            //if (subblockbtr.IsFromExternalReference) continue;
                            // Extract nozzle name
                            string nozzleName = subblockref.Name;

                            
                            if (nozzleName.Equals("$1$ASME B16.5 Flange Welding Neck - Class 150 2"))
                            {
                                //subblockTransform = subblockref.BlockTransform;
                                // Extract base point
                                Point3d basePoint = subblockref.Position.TransformBy(blockTransf);
                                ed.WriteMessage($"\tbasepoint: ({basePoint.X},{basePoint.Y},{basePoint.Z})");
                                // Calculate center of bounding box
                                Extents3d extents = subblockref.GeometricExtents;//blocktransform?: subblockref.BlockTransform

                                Point3d center = (extents.MinPoint.Add(extents.MaxPoint.GetAsVector()) / 2.0).TransformBy(blockTransf);

                                Vector3d dir = center.GetVectorTo(basePoint);

                                LineSegment3d extendedLine = new LineSegment3d();
                                extendedLine.Set(basePoint, dir);

                                Point3d p3 = extendedLine.EndPoint;
                                // Add information to the list
                                blockInfoList.Add(new object[] { nozzleName, basePoint, p3 });
                            }
                        }

                    }

                    //start eqpconv
                    //
                    using (EquipmentHelper eqHelper = new EquipmentHelper())
                    {
                        // New type
                        //
                        EquipmentType eqt = eqHelper.NewImportedProjectEquipment(eqClass);

                        // Convert
                        //
                        PartSizeProperties equipPart;
                        Equipment eqEnt = eqHelper.CreateEquipmentEntity(eqt, new ObjectId[] { blockId }, blockbasePoint, out equipPart);
                        if (eqEnt == null)
                        {
                            ed.WriteMessage("\nCan't convert equipment entity");
                            return;
                        }


                        // Add
                        //
                        Project currentProject = PlantApp.CurrentProject.ProjectParts["Piping"];
                        DataLinksManager dlm = currentProject.DataLinksManager;
                        DataLinksManager3d dlm3d = DataLinksManager3d.Get3dManager(dlm);

                        using (PipingObjectAdder pipeObjAdder = new PipingObjectAdder(dlm3d, db))
                        {
                            pipeObjAdder.Add(equipPart, eqEnt, null);
                            tr.AddNewlyCreatedDBObject(eqEnt, true);
                        }
                        //end eqpconv


                        //addnozzle start
                        foreach (object[] info in blockInfoList)
                        {
                            string nozzle = info[0].ToString();
                            Point3d basePt = (Point3d)info[1];

                            Point3d centerPt = (Point3d)info[2];


                            /*ed.WriteMessage($"\nNozzle Name: {nozzle}");
                            ed.WriteMessage($"\tBase Point: ({basePt.X}, {basePt.Y}, {basePt.Z})");
                            ed.WriteMessage($"\tCenter Bounding Box: ({centerPt.X}, {centerPt.Y}, {centerPt.Z})");*/

                            //Nozzle, flanged, 50 ND, C, 10, DIN 2632
                            int newIndex = eqHelper.NewNozzleIndex(eqt);
                            String nozName = "New Nozzle " + newIndex.ToString();
                            NozzleInfo ni = eqHelper.NewNozzle(eqt, newIndex, nozName);
                            // Metric
                            //
                            NominalDiameter nd = new NominalDiameter();
                            nd.EUnits = Units.Mm;//Undefined, Inch, Mm
                            nd.Value = 50;
                            string pressureClass = "10";
                            string facing = "C";
                            string endtype = "FL";

                            // For example, straight, flanged
                            //
                            PnPRow[] rows = NozzleInfo.SelectFromNozzleCatalog(eqHelper.NozzleRepository, "StraightNozzle", nd, endtype, pressureClass, facing);
                            if (rows.Length == 0)
                            {
                                ed.WriteMessage("\nNo nozzles found in the catalog.");
                                return;
                            }
                            else
                            {
                                foreach (var row in rows)
                                {
                                    ed.WriteMessage("\nnozzle found: " + row[PartsRepository.LongDescription].ToString());
                                    if (row[PartsRepository.LongDescription].ToString().Contains(nozzle))
                                    { 
                                        //guid = row[PartsRepository.PartGuid].ToString();
                                        break;
                                    }
                                }
                                
                            }

                            // We need equipment ECS
                            //
                            Matrix3d ecs = Matrix3d.Identity;
                            ecs = eqEnt.Ecs;

                            // Take the first
                            // Its guid
                            //
                            String guid = String.Empty;
                            guid = rows[0][PartsRepository.PartGuid].ToString();
                            //ed.WriteMessage("\nguid: " + guid);
                            // Assign nozzle part
                            //
                            bool setnozzlepart = eqHelper.SetNozzlePart(eqt, ni, guid);
                            ed.WriteMessage("\nsetnozzlepart: " + setnozzlepart);
                            bool setnozzlelocation = eqHelper.SetNozzleLocation(eqt, ni, basePt, centerPt, 0.0, ecs);
                            ed.WriteMessage("\nsetnozzlelocation: " + setnozzlelocation);
                            // Add new nozzle
                            //
                            eqt.Nozzles.Add(ni);

                        }//addnozzle end
                        bool updateequipmententity = eqHelper.UpdateEquipmentEntity(eqEnt.ObjectId, eqt, null, 1.0);
                        ed.WriteMessage("\nupdateequipmententity: " + updateequipmententity);

                    }
                    



                }

                else
                {
                    ed.WriteMessage("\nSelected entity is not a block reference.");
                }

                tr.Commit();
            }
        }



    }


}
