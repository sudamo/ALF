using System;
using System.Data;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Configuration;

namespace ALFWebService
{
    static class Common
    {
        #region STATIC
        private static string C_CONNECTIONSTRING;
        static Common()
        {
            C_CONNECTIONSTRING = ConfigurationManager.AppSettings["C_CONNECTIONSTRING"];
        }
        #endregion

        #region 连接测试
        /// <summary>
        /// 连接测试
        /// </summary>
        /// <returns>连接成功/连接失败</returns>
        public static string TestConnection()
        {
            bool bReturn = false;
            SqlConnection conn = new SqlConnection(C_CONNECTIONSTRING);
            try
            {
                conn.Open();
                if (conn.State == ConnectionState.Open)
                    bReturn = true;
            }
            catch
            {
                bReturn = false;
            }
            finally
            {
                conn.Close();
            }
            return bReturn ? "连接成功" : "连接失败";
        }
        #endregion

        #region 审核单据
        /// <summary>
        /// 审核单据 - 目前只支持 ICStockBill 单据
        /// </summary>
        /// <param name="pFBillNo">单号</param>
        /// <param name="pFCheckerID">审核人ID</param>
        /// <returns>审核成功/审核失败：失败信息</returns>
        public static string AuditBill(string pFBillNo, int pFCheckerID)
        {
            if (pFBillNo.Trim() == string.Empty)
                return "no@x000:参数必填。";

            string strSQL = @"SELECT A.FBillNo,A.FTranType,A.FStatus,AE.FItemID,MTL.FNumber,AE.FQty,ISNULL(INV.FQty,0) FStockQty,ISNULL(AE.FDCStockID,0)FDCStockID,ISNULL(AE.FDCSPID,0) FDCSPID,ISNULL(AE.FSCStockID,0) FSCStockID,ISNULL(AE.FSCSPID,0) FSCSPID,AE.FBatchNo,AE.FSourceBillNo,AE.FSourceInterId,AE.FSourceEntryID,MTL.FBatchManager,A.FROB
            FROM ICStockBill A
            INNER JOIN ICStockBillEntry AE ON A.FInterID = AE.FInterID
            INNER JOIN t_ICItem MTL ON AE.FItemID = MTL.FItemID
            LEFT JOIN ICInventory INV ON AE.FItemID = INV.FItemID AND AE.FBatchNo = INV.FBatchNo AND AE.FDCStockID = INV.FStockID AND AE.FDCSPID = INV.FStockPlaceID
            WHERE A.FBillNo = '" + pFBillNo + "'";

            object obj = SqlOperation(3, strSQL);

            if (obj == null || ((DataTable)obj).Rows.Count == 0)
                return "审核失败：单据不存在。";

            DataTable dt = (DataTable)obj;

            if (dt.Rows[0]["FStatus"].ToString() == "1")
                return "审核失败：单据已经审核。";

            List<int> lstTranType = new List<int>();
            //lstTranType.Add(1);//外购入库 WIN
            //lstTranType.Add(2);//产品入库 CIN
            lstTranType.Add(10);//其他入库 QIN
            //lstTranType.Add(21);//销售出库 XOUT
            //lstTranType.Add(24);//生产领料 SOUT
            lstTranType.Add(29);//其他出库 QOUT
            //lstTranType.Add(41);//仓库调拨 CHG

            if (!lstTranType.Contains(int.Parse(dt.Rows[0]["FTranType"].ToString())))
            {
                return "审核失败：仅支持其他入库、其他出库。";
            }

            #region 库存判断
            if (int.Parse(dt.Rows[0]["FTranType"].ToString()) == 29)//非其他入库
            {
                for (int i = 0; i < dt.Rows.Count; i++)
                {
                    if (decimal.Parse(dt.Rows[i]["FQty"].ToString()) > decimal.Parse(dt.Rows[i]["FStockQty"].ToString()))
                        return "审核失败：物料[" + dt.Rows[i]["FNumber"].ToString() + "]需求数量[" + dt.Rows[i]["FQty"].ToString() + "]大于库存数量[" + dt.Rows[i]["FStockQty"].ToString() + "]";
                }
            }
            #endregion

            #region 反写库存
            try
            {
                switch (int.Parse(dt.Rows[0]["FTranType"].ToString()))
                {
                    case 10://其他入库
                        for (int i = 0; i < dt.Rows.Count; i++)
                        {
                            if (dt.Rows[i]["FDCSPID"] == null || dt.Rows[i]["FDCSPID"].ToString() == "0")
                                strSQL = @"MERGE INTO ICInventory AS IC
                                USING
                                (
                                    SELECT " + dt.Rows[i]["FItemID"].ToString() + " FItemID," + dt.Rows[i]["FQty"].ToString() + " FQty,'" + dt.Rows[i]["FBatchNo"].ToString() + "' FBatchNo, " + dt.Rows[i]["FDCStockID"].ToString() + " FStockID, " + dt.Rows[i]["FDCSPID"].ToString() + @" FSPID
                                ) AS DT ON IC.FItemID = DT.FItemID AND IC.FStockID = DT.FStockID AND IC.FBatchNo = DT.FBatchNo
                                WHEN MATCHED
                                    THEN UPDATE SET FQty = IC.FQty + DT.FQty
                                WHEN NOT MATCHED
                                    THEN INSERT(FBrNo,FItemID,FStockID,FStockPlaceID,FQty,FBatchNo) VALUES(0,DT.FItemID,DT.FStockID,DT.FSPID,DT.FQty,DT.FBatchNo);";
                            else
                                strSQL = @"MERGE INTO ICInventory AS IC
                                USING
                                (
                                    SELECT " + dt.Rows[i]["FItemID"].ToString() + " FItemID," + dt.Rows[i]["FQty"].ToString() + " FQty,'" + dt.Rows[i]["FBatchNo"].ToString() + "' FBatchNo, " + dt.Rows[i]["FDCStockID"].ToString() + " FStockID, " + dt.Rows[i]["FDCSPID"].ToString() + @" FSPID
                                ) AS DT ON IC.FItemID = DT.FItemID AND IC.FStockID = DT.FStockID AND IC.FBatchNo = DT.FBatchNo AND IC.FStockPlaceID = DT.FSPID
                                WHEN MATCHED
                                    THEN UPDATE SET FQty = IC.FQty + DT.FQty
                                WHEN NOT MATCHED
                                    THEN INSERT(FBrNo,FItemID,FStockID,FStockPlaceID,FQty,FBatchNo) VALUES(0,DT.FItemID,DT.FStockID,DT.FSPID,DT.FQty,DT.FBatchNo);";

                            SqlOperation(0, strSQL);
                        }
                        break;
                    case 29://其他出库
                        {
                            //对即时库存的判断
                            for (int i = 0; i < dt.Rows.Count; i++)
                            {
                                if (dt.Rows[i]["FDCSPID"] == null || dt.Rows[i]["FDCSPID"].ToString() == "0")
                                    strSQL = "SELECT FQty FROM ICInventory WHERE FItemID = " + dt.Rows[i]["FItemID"].ToString() + " AND FStockID = " + dt.Rows[i]["FDCStockID"].ToString() + " AND FBatchNo = '" + dt.Rows[i]["FBatchNo"].ToString() + "'";
                                else
                                    strSQL = "SELECT FQty FROM ICInventory WHERE FItemID = " + dt.Rows[i]["FItemID"].ToString() + " AND FStockID = " + dt.Rows[i]["FDCStockID"].ToString() + " AND FStockPlaceID = " + dt.Rows[i]["FDCSPID"].ToString() + " AND FBatchNo = '" + dt.Rows[i]["FBatchNo"].ToString() + "'";

                                obj = SqlOperation(1, strSQL);

                                if (obj == null)
                                    return "审核失败：" + "物料[" + dt.Rows[i]["FItem"].ToString() + "]未能匹配到即时库存";

                                if ((decimal)obj < decimal.Parse(dt.Rows[i]["FQty"].ToString()))
                                    return "审核失败：" + "物料[" + dt.Rows[i]["FItem"].ToString() + "]即时库存[" + ((decimal)obj).ToString() + "]小于出库数量[" + dt.Rows[i]["FQty"].ToString() + "]";
                            }

                            //反写库存
                            for (int i = 0; i < dt.Rows.Count; i++)
                            {
                                if (dt.Rows[i]["FDCSPID"] == null || dt.Rows[i]["FDCSPID"].ToString() == "0")
                                    strSQL = "UPDATE ICInventory SET FQty = FQty - " + dt.Rows[i]["FQty"].ToString() + " WHERE FItemID = " + dt.Rows[i]["FItemID"].ToString() + " AND FBatchNo = '" + dt.Rows[i]["FBatchNo"].ToString() + "' AND FStockID = " + dt.Rows[i]["FDCStockID"].ToString();
                                else
                                    strSQL = "UPDATE ICInventory SET FQty = FQty - " + dt.Rows[i]["FQty"].ToString() + " WHERE FItemID = " + dt.Rows[i]["FItemID"].ToString() + " AND FBatchNo = '" + dt.Rows[i]["FBatchNo"].ToString() + "' AND FStockID = " + dt.Rows[i]["FDCStockID"].ToString() + " AND FStockPlaceID = " + dt.Rows[i]["FDCSPID"].ToString();

                                SqlOperation(0, strSQL);
                            }
                        }
                        break;
                        //default:
                        //    {
                        //        strSQL = "SELECT '未知单据'";
                        //    }
                        //    break;
                }
            }
            catch (Exception ex)
            {
                return "审核失败：" + ex.Message;
            }
            #endregion

            //Execute Audit Bill
            strSQL = "UPDATE ICStockBill SET FCheckDate = GETDATE(),FStatus = 1,FNote = 'WMS_Audit',FCheckerID = " + pFCheckerID.ToString() + " WHERE FBillNo = '" + pFBillNo + "'";
            SqlOperation(0, strSQL);

            return "审核成功";
        }
        #endregion

        #region 外购入库单
        /// <summary>
        /// 外购入库单
        /// </summary>
        /// <param name="pHead">表头：FDeptID|FSManagerID|FFManagerID|FBillerID|FPOOrderBillNo|FNote</param>
        /// <param name="pDetails">表体：[FItemNumber|FDCStockNumber|FDCSPNumber|FBatchNo|FQty|FSourceBillNo|FNote],......</param>
        /// <returns>yes@ID:xxxx;Number:xxxx/no@ExceptionMessage</returns>
        public static string ICStockBillForPO(string pHead, string pDetails)
        {
            object obj;
            string strSQL;
            DataTable dtDtl;
            DataRow dr;
            SqlConnection conn;

            if (pHead.Trim() == string.Empty || pDetails.Trim() == string.Empty)
                return "no@x000:参数必填。";

            int FInterID;
            string FBillNo;

            GetICMaxIDAndBillNo(1, out FInterID, out FBillNo);

            if (FBillNo.IndexOf("Error") >= 0)
                return "no@x001:" + FBillNo;

            //POOrder
            int POFInterID, POFEntryID;
            DataTable dtCheck;

            //定义表头字段
            string FNote, FPOOrderBillNo;
            int FDeptID, FSupplyID, FSManagerID, FFManagerID, FBillerID, POFInterIDH, FHeadSelfP0255, FHeadSelfP0256;

            //定义表体字段
            int FItemID, FUnitID, FDCStockID, FDCSPID;
            decimal FPrice, FQty, FStockQty;
            string FNoteD, FItem, FDCStock, FDCSP, FBatchNo, FSourceBillNo;

            //物料
            bool FBatchManager = false;

            #region 字段赋值
            try
            {
                FDeptID = int.Parse(pHead.Substring(0, pHead.IndexOf("|")));//部门
                pHead = pHead.Substring(pHead.IndexOf("|") + 1);
                FSManagerID = int.Parse(pHead.Substring(0, pHead.IndexOf("|"))) == 0 ? 16429 : int.Parse(pHead.Substring(0, pHead.IndexOf("|")));//保管人
                pHead = pHead.Substring(pHead.IndexOf("|") + 1);
                FFManagerID = int.Parse(pHead.Substring(0, pHead.IndexOf("|"))) == 0 ? 16429 : int.Parse(pHead.Substring(0, pHead.IndexOf("|")));//验收人
                pHead = pHead.Substring(pHead.IndexOf("|") + 1);
                FBillerID = int.Parse(pHead.Substring(0, pHead.IndexOf("|")));//制单人
                pHead = pHead.Substring(pHead.IndexOf("|") + 1);
                FPOOrderBillNo = pHead.Substring(0, pHead.IndexOf("|"));//采购订单编号
                //
                FNote = pHead.Substring(pHead.IndexOf("|") + 1);//备注

                strSQL = "SELECT FInterID,FSupplyID,FHeadSelfP0255,ISNULL(FHeadSelfP0256,5367) FHeadSelfP0256 FROM POOrder WHERE FBillNo = '" + FPOOrderBillNo + "'";
                //obj = SqlOperation(3, "SELECT FInterID,FSupplyID FROM POOrder WHERE FBillNo = '" + FPOOrderBillNo + "'");

                obj = SqlOperation(3, strSQL);
                if (obj == null || ((DataTable)obj).Rows.Count == 0)
                    return "no@没有此采购订单数据[" + FPOOrderBillNo + "]";

                POFInterIDH = int.Parse(((DataTable)obj).Rows[0]["FInterID"].ToString());
                FSupplyID = int.Parse(((DataTable)obj).Rows[0]["FSupplyID"].ToString());
                FHeadSelfP0255 = int.Parse(((DataTable)obj).Rows[0]["FHeadSelfP0255"].ToString());
                FHeadSelfP0256 = int.Parse(((DataTable)obj).Rows[0]["FHeadSelfP0256"].ToString());

                //
                dtDtl = new DataTable();
                dtDtl.Columns.Add("FItem");
                dtDtl.Columns.Add("FItemID");//物料ID
                dtDtl.Columns.Add("FUnitID");//单位ID
                dtDtl.Columns.Add("FDCStockID");//仓库
                dtDtl.Columns.Add("FDCSPID");//仓位

                dtDtl.Columns.Add("FBatchNo");//批次号
                dtDtl.Columns.Add("FQty");//未入库数量
                dtDtl.Columns.Add("Fprice");//单价
                dtDtl.Columns.Add("FAmount");//总金额
                dtDtl.Columns.Add("FSourceBillNo");//采购订单号

                dtDtl.Columns.Add("FSourceInterId");//采购订单内码
                dtDtl.Columns.Add("FSourceEntryID");//采购订单分录内码
                dtDtl.Columns.Add("FNote");//备注
                dtDtl.Columns.Add("FStockQty");//入库数量

                string strTemp;
                do
                {
                    if (pDetails.IndexOf("],[") > 0)
                    {
                        strTemp = pDetails.Substring(0, pDetails.IndexOf("]") + 1);
                        pDetails = pDetails.Substring(pDetails.IndexOf("]") + 2);
                    }
                    else
                    {
                        strTemp = pDetails;
                        pDetails = "";
                    }

                    FItem = strTemp.Substring(1, strTemp.IndexOf("|") - 1);//MTLNumber
                    strTemp = strTemp.Substring(strTemp.IndexOf("|") + 1);
                    FDCStock = strTemp.Substring(0, strTemp.IndexOf("|"));//FStockNumber
                    strTemp = strTemp.Substring(strTemp.IndexOf("|") + 1);
                    FDCSP = strTemp.Substring(0, strTemp.IndexOf("|"));//FSPNumber
                    strTemp = strTemp.Substring(strTemp.IndexOf("|") + 1);
                    FBatchNo = strTemp.Substring(0, strTemp.IndexOf("|"));//FBatchNo
                    strTemp = strTemp.Substring(strTemp.IndexOf("|") + 1);
                    FStockQty = decimal.Parse(strTemp.Substring(0, strTemp.IndexOf("|")));//FStockQty

                    strTemp = strTemp.Substring(strTemp.IndexOf("|") + 1);
                    FSourceBillNo = strTemp.Substring(0, strTemp.IndexOf("|"));//POOrderBillNo
                    //
                    strTemp = strTemp.Substring(strTemp.IndexOf("|") + 1);
                    FNoteD = strTemp.Substring(0, strTemp.IndexOf("]"));//FNoteD

                    strSQL = "SELECT A.FInterID FSourceInterId,AE.FEntryID FSourceEntryID,A.FSupplyID,AE.FItemID,MTL.FUnitID,AE.FPrice,MTL.FBatchManager,AE.FQty - AE.FStockQty FQty FROM POOrder A INNER JOIN POOrderEntry AE ON A.FInterID = AE.FInterID INNER JOIN t_ICItem MTL ON AE.FItemID = MTL.FItemID WHERE A.FBillNo = '" + FSourceBillNo + "' AND MTL.FNumber = '" + FItem + "'";

                    //strSQL = @"SELECT A.FInterID FSourceInterId,AE.FEntryID FSourceEntryID,A.FSupplyID,AE.FItemID,MTL.FUnitID,AE.FPrice,MTL.FBatchManager,O.FQty
                    //FROM POOrder A
                    //INNER JOIN POOrderEntry AE ON A.FInterID = AE.FInterID
                    //INNER JOIN t_ICItem MTL ON AE.FItemID = MTL.FItemID
                    //INNER JOIN 
                    //(
                    //SELECT FInterID,FItemID,SUM(FQty - FStockQty) FQty
                    //FROM POOrderEntry
                    //GROUP BY FInterID,FItemID
                    //)O ON O.FInterID = AE.FInterID AND O.FItemID = AE.FItemID
                    //WHERE A.FBillNo = '" + FSourceBillNo + "' AND MTL.FNumber = '" + FItem + "'";

                    obj = SqlOperation(3, strSQL);
                    if (obj == null || ((DataTable)obj).Rows.Count == 0)
                        return "no@未找到物料信息[" + FSourceBillNo + "].[" + FItem + "]";

                    POFInterID = int.Parse(((DataTable)obj).Rows[0]["FSourceInterId"].ToString());//FInterID
                    POFEntryID = int.Parse(((DataTable)obj).Rows[0]["FSourceEntryID"].ToString());//FEntryID
                    FItemID = int.Parse(((DataTable)obj).Rows[0]["FItemID"].ToString());//MTLID
                    FUnitID = int.Parse(((DataTable)obj).Rows[0]["FUnitID"].ToString());//UnitID
                    FPrice = decimal.Parse(((DataTable)obj).Rows[0]["FPrice"].ToString());//Price
                    FQty = decimal.Parse(((DataTable)obj).Rows[0]["FQty"].ToString());//FQty

                    FBatchManager = ((DataTable)obj).Rows[0]["FBatchManager"].ToString() == "0" ? false : true;//是否采用业务批次管理

                    if (FSupplyID != int.Parse(((DataTable)obj).Rows[0]["FSupplyID"].ToString()))
                    {
                        return "no@表头[" + FPOOrderBillNo + "]的供应商与表体[" + FSourceBillNo + "]的供应商不一致。";
                    }

                    if (FStockQty > FQty)
                    {
                        return "no@[" + FSourceBillNo + "].[" + FItem + "]物料本次入库数量[" + FStockQty.ToString() + "]大于采购订单的未入库数量[" + FQty.ToString() + "]。";
                    }

                    if (FDCStock == "")
                        FDCStockID = 0;
                    else
                    {
                        obj = SqlOperation(3, "SELECT FItemID FROM t_Stock WHERE FNumber = '" + FDCStock + "'");
                        if (obj == null || ((DataTable)obj).Rows.Count == 0)
                            return "no@未找到仓库信息[" + FDCStock + "]";
                        FDCStockID = int.Parse(((DataTable)obj).Rows[0]["FItemID"].ToString());//FStockID
                    }

                    if (FDCSP == "")
                        FDCSPID = 0;
                    else
                    {
                        obj = SqlOperation(3, "SELECT FSPID FROM t_StockPlace WHERE FNumber = '" + FDCSP + "'");
                        if (obj == null || ((DataTable)obj).Rows.Count == 0)
                            return "no@未找到仓位信息[" + FDCSP + "]";
                        FDCSPID = int.Parse(((DataTable)obj).Rows[0]["FSPID"].ToString());//FDCSPID
                    }

                    if (FBatchNo.Equals(string.Empty) && FBatchManager)
                    {
                        return "no@[" + FItem + "]物料已经启用批次号管理，请携带批次号。";
                    }

                    dr = dtDtl.NewRow();
                    dr["FItem"] = FItem;//物料编码

                    dr["FItemID"] = FItemID;//物料ID
                    dr["FUnitID"] = FUnitID;//单位ID
                    dr["FDCStockID"] = FDCStockID;//仓库ID
                    dr["FDCSPID"] = FDCSPID;//仓位ID
                    dr["FBatchNo"] = FBatchNo;//批次号

                    dr["FQty"] = FQty;//采购数量
                    dr["Fprice"] = FPrice;//单价
                    dr["FAmount"] = FStockQty * FPrice;//金额
                    dr["FSourceBillNo"] = FSourceBillNo;//采购订单
                    dr["FSourceInterId"] = POFInterID;//采购订单内码

                    dr["FSourceEntryID"] = POFEntryID;//采购订单明显内码
                    dr["FNote"] = FNoteD;//备注
                    dr["FStockQty"] = FStockQty;//入库数量

                    dtDtl.Rows.Add(dr);
                }
                while (pDetails.Length > 0);
            }
            catch (Exception ex)
            {
                return "no@x002:" + ex.Message;
            }
            #endregion

            #region 对入库数量的判断
            dtCheck = dtDtl.Clone();//获取汇总FQty的物料信息
            dtCheck.ImportRow(dtDtl.Rows[0]);//把第一条数据写入dtPO中
            //汇总入库情况
            for (int i = 1; i < dtDtl.Rows.Count; i++)
            {
                for (int j = 0; j < dtCheck.Rows.Count; j++)//根据采购单号与物料编码进行匹配
                {
                    if (dtCheck.Rows[j]["FSourceBillNo"].ToString() == dtDtl.Rows[i]["FSourceBillNo"].ToString() && dtCheck.Rows[j]["FItem"].ToString() == dtDtl.Rows[i]["FItem"].ToString())
                    {
                        dtCheck.Rows[j]["FStockQty"] = decimal.Parse(dtCheck.Rows[j]["FStockQty"].ToString()) + decimal.Parse(dtDtl.Rows[i]["FStockQty"].ToString());
                        break;
                    }

                    if (j == dtCheck.Rows.Count - 1)//未匹配到数据
                    {
                        dtCheck.ImportRow(dtDtl.Rows[i]);
                        break;
                    }
                }
            }

            for (int i = 0; i < dtCheck.Rows.Count; i++)
            {
                if (decimal.Parse(dtCheck.Rows[i]["FStockQty"].ToString()) > decimal.Parse(dtCheck.Rows[i]["FQty"].ToString()))//入库数量大于采购数量
                {
                    return "no@[" + dtCheck.Rows[i]["FSourceBillNo"].ToString() + "].[" + dtCheck.Rows[i]["FItem"].ToString() + "]物料本次总入库数量[" + dtCheck.Rows[i]["FStockQty"].ToString() + "]大于未入库数量[" + dtCheck.Rows[i]["FQty"].ToString() + "]。";
                }
            }
            #endregion

            conn = new SqlConnection(C_CONNECTIONSTRING);

            #region 插入主表
            try
            {
                conn.Open();
                SqlCommand cmdH = conn.CreateCommand();
                cmdH.CommandType = CommandType.Text;

                cmdH.Parameters.Add("@FInterID", SqlDbType.Int);
                cmdH.Parameters.Add("@FBillNo", SqlDbType.NVarChar, 255);
                cmdH.Parameters.Add("@FNote", SqlDbType.NVarChar, 255);
                cmdH.Parameters.Add("@FDeptID", SqlDbType.Int);
                cmdH.Parameters.Add("@FSupplyID", SqlDbType.Int);

                cmdH.Parameters.Add("@FSManagerID", SqlDbType.Int);
                cmdH.Parameters.Add("@FFManagerID", SqlDbType.Int);
                cmdH.Parameters.Add("@FBillerID", SqlDbType.Int);
                cmdH.Parameters.Add("@FOrgBillInterID", SqlDbType.Int);
                cmdH.Parameters.Add("@FPOOrderBillNo", SqlDbType.NVarChar);

                cmdH.Parameters.Add("@FHeadSelfP0255", SqlDbType.Int);
                cmdH.Parameters.Add("@FHeadSelfP0256", SqlDbType.Int);

                cmdH.Parameters["@FInterID"].Value = FInterID;
                cmdH.Parameters["@FBillNo"].Value = FBillNo;
                cmdH.Parameters["@FNote"].Value = FNote;
                cmdH.Parameters["@FDeptID"].Value = FDeptID;
                cmdH.Parameters["@FSupplyID"].Value = FSupplyID;

                cmdH.Parameters["@FSManagerID"].Value = FSManagerID;
                cmdH.Parameters["@FFManagerID"].Value = FFManagerID;
                cmdH.Parameters["@FBillerID"].Value = FBillerID;
                cmdH.Parameters["@FOrgBillInterID"].Value = POFInterIDH;
                cmdH.Parameters["@FPOOrderBillNo"].Value = FPOOrderBillNo;

                cmdH.Parameters["@FHeadSelfP0255"].Value = FHeadSelfP0255;
                cmdH.Parameters["@FHeadSelfP0256"].Value = FHeadSelfP0256;

                strSQL = @"INSERT INTO dbo.ICStockBill(FInterID,FBillNo,FBrNo,FTranType,FROB,FNote,FDate,FDeptID,FSupplyID,FPurposeID,   FSManagerID,FFManagerID,FBillerID,FCheckerID,FCheckDate,FStatus,FSelTranType,FPOMode,FPOStyle,FOrgBillInterID,FPOOrdBillNo,FHeadSelfA0143,FHeadSelfA0144) 
                VALUES (@FInterID,@FBillNo,'0',1,1,@FNote,CONVERT(VARCHAR(10),GETDATE(),120),@FDeptID,@FSupplyID,0,  @FSManagerID,@FFManagerID,@FBillerID,@FBillerID,GETDATE(),1,71,36680,252,@FOrgBillInterID,@FPOOrderBillNo,@FHeadSelfP0255,@FHeadSelfP0256)";

                cmdH.CommandText = strSQL;
                cmdH.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                return "no@x003:" + ex.Message;
            }
            finally
            {
                conn.Close();
            }
            #endregion

            #region 插入表体
            conn.Open();
            SqlCommand cmdD = conn.CreateCommand();
            cmdD.CommandType = CommandType.Text;

            cmdD.Parameters.Add("@FInterID", SqlDbType.Int);
            cmdD.Parameters.Add("@FEntryID", SqlDbType.Int);
            cmdD.Parameters.Add("@FItemID", SqlDbType.Int);
            cmdD.Parameters.Add("@FBatchNo", SqlDbType.VarChar);
            cmdD.Parameters.Add("@FQty", SqlDbType.Decimal);

            cmdD.Parameters.Add("@FUnitID", SqlDbType.Int);
            cmdD.Parameters.Add("@FDCStockID", SqlDbType.Int);
            cmdD.Parameters.Add("@FDCSPID", SqlDbType.Int);
            cmdD.Parameters.Add("@FSourceBillNo", SqlDbType.VarChar, 50);
            cmdD.Parameters.Add("@FSourceInterId", SqlDbType.Int);

            cmdD.Parameters.Add("@FSourceEntryID", SqlDbType.Int);
            cmdD.Parameters.Add("@Fprice", SqlDbType.Decimal);
            cmdD.Parameters.Add("@FAmount", SqlDbType.Decimal);
            cmdD.Parameters.Add("@FNote", SqlDbType.VarChar);

            for (int i = 0; i < dtDtl.Rows.Count; i++)
            {
                try
                {
                    cmdD.Parameters["@FInterID"].Value = FInterID;
                    cmdD.Parameters["@FEntryID"].Value = i + 1;
                    cmdD.Parameters["@FItemID"].Value = dtDtl.Rows[i]["FItemID"].ToString();
                    cmdD.Parameters["@FBatchNo"].Value = dtDtl.Rows[i]["FBatchNo"].ToString();
                    cmdD.Parameters["@FQty"].Value = dtDtl.Rows[i]["FStockQty"].ToString();

                    cmdD.Parameters["@FUnitID"].Value = dtDtl.Rows[i]["FUnitID"].ToString();
                    cmdD.Parameters["@FDCStockID"].Value = dtDtl.Rows[i]["FDCStockID"].ToString();
                    cmdD.Parameters["@FDCSPID"].Value = dtDtl.Rows[i]["FDCSPID"].ToString();
                    cmdD.Parameters["@FSourceBillNo"].Value = dtDtl.Rows[i]["FSourceBillNo"].ToString();
                    cmdD.Parameters["@FSourceInterId"].Value = dtDtl.Rows[i]["FSourceInterId"].ToString();

                    cmdD.Parameters["@FSourceEntryID"].Value = dtDtl.Rows[i]["FSourceEntryID"].ToString();
                    cmdD.Parameters["@Fprice"].Value = dtDtl.Rows[i]["Fprice"].ToString();
                    cmdD.Parameters["@FAmount"].Value = dtDtl.Rows[i]["FAmount"].ToString();
                    cmdD.Parameters["@FNote"].Value = dtDtl.Rows[i]["FNote"].ToString();

                    strSQL = @"INSERT INTO dbo.ICstockbillentry(FInterID,FEntryID,FBrNo,FItemID,FBatchNo,FQty,FQtyMust,FUnitID,FAuxQtyMust,Fauxqty, FDCStockID,FDCSPID,FSourceTranType,FSourceBillNo,FSourceInterId,FSourceEntryID,FOrderBillno,FOrderInterID,FOrderEntryID,FOrgBillEntryID,    FChkPassItem,Fconsignprice,FconsignAmount,Fprice,Fauxprice,FAmount,FNote)
                    VALUES(@FInterID,@FEntryID,'0',@FItemID,@FBatchNo,@FQty,@FQty,@FUnitID,@FQty,@FQty, @FDCStockID,@FDCSPID,71,@FSourceBillNo,@FSourceInterId,@FSourceEntryID,@FSourceBillNo,@FSourceInterId,@FSourceEntryID,@FSourceEntryID,    1058,@Fprice,@FAmount,@Fprice,@Fprice,@FAmount,@FNote)";

                    cmdD.CommandText = strSQL;
                    cmdD.ExecuteNonQuery();
                }
                catch (Exception ex)
                {
                    //删除已生成的单据数据
                    SqlOperation(0, "DELETE FROM ICStockBill WHERE FInterID = " + FInterID.ToString() + " DELETE FROM ICstockbillentry WHERE FInterID = " + FInterID.ToString());

                    conn.Close();
                    return "no@x004:" + ex.Message;
                }
            }
            #endregion

            #region 反写库存和采购订单
            for (int i = 0; i < dtDtl.Rows.Count; i++)
            {
                if (dtDtl.Rows[i]["FDCSPID"] == null || dtDtl.Rows[i]["FDCSPID"].ToString() == "0")
                    strSQL = @"MERGE INTO ICInventory AS IC
                    USING
                    (
                        SELECT " + dtDtl.Rows[i]["FItemID"].ToString() + " FItemID, " + dtDtl.Rows[i]["FDCStockID"].ToString() + " FStockID," + dtDtl.Rows[i]["FDCSPID"].ToString() + " FSPID," + dtDtl.Rows[i]["FStockQty"].ToString() + " FQty,'" + dtDtl.Rows[i]["FBatchNo"].ToString() + @"' FBatchNo
                    ) AS DT ON IC.FItemID = DT.FItemID AND IC.FStockID = DT.FStockID AND IC.FBatchNo = DT.FBatchNo
                    WHEN MATCHED
                        THEN UPDATE SET FQty = IC.FQty + DT.FQty
                    WHEN NOT MATCHED
                        THEN INSERT(FBrNo,FItemID,FStockID,FStockPlaceID,FQty,FBatchNo) VALUES(0,DT.FItemID,DT.FStockID,DT.FSPID,DT.FQty,DT.FBatchNo);
                    UPDATE POOrderEntry SET FStockQty =  FStockQty + " + dtDtl.Rows[i]["FStockQty"].ToString() + ",FAuxStockQty =  FAuxStockQty + " + dtDtl.Rows[i]["FStockQty"].ToString() + ",FSecStockQty =  FSecStockQty + " + dtDtl.Rows[i]["FStockQty"].ToString() + ",FCommitQty =  FCommitQty + " + dtDtl.Rows[i]["FStockQty"].ToString() + ",FAuxCommitQty =  FAuxCommitQty + " + dtDtl.Rows[i]["FStockQty"].ToString() + ",FSecCommitQty =  FSecCommitQty + " + dtDtl.Rows[i]["FStockQty"].ToString() + " WHERE FInterID = " + dtDtl.Rows[i]["FSourceInterId"].ToString() + " AND FEntryID = " + dtDtl.Rows[i]["FSourceEntryID"].ToString() + ";";
                else
                    strSQL = @"MERGE INTO ICInventory AS IC
                    USING
                    (
                        SELECT " + dtDtl.Rows[i]["FItemID"].ToString() + " FItemID, " + dtDtl.Rows[i]["FDCStockID"].ToString() + " FStockID," + dtDtl.Rows[i]["FDCSPID"].ToString() + " FSPID," + dtDtl.Rows[i]["FStockQty"].ToString() + " FQty,'" + dtDtl.Rows[i]["FBatchNo"].ToString() + @"' FBatchNo
                    ) AS DT ON IC.FItemID = DT.FItemID AND IC.FStockID = DT.FStockID AND IC.FBatchNo = DT.FBatchNo AND IC.FStockPlaceID = DT.FSPID
                    WHEN MATCHED
                        THEN UPDATE SET FQty = IC.FQty + DT.FQty
                    WHEN NOT MATCHED
                        THEN INSERT(FBrNo,FItemID,FStockID,FStockPlaceID,FQty,FBatchNo) VALUES(0,DT.FItemID,DT.FStockID,DT.FSPID,DT.FQty,DT.FBatchNo);
                    UPDATE POOrderEntry SET FStockQty =  FStockQty + " + dtDtl.Rows[i]["FStockQty"].ToString() + ",FAuxStockQty =  FAuxStockQty + " + dtDtl.Rows[i]["FStockQty"].ToString() + ",FSecStockQty =  FSecStockQty + " + dtDtl.Rows[i]["FStockQty"].ToString() + ",FCommitQty =  FCommitQty + " + dtDtl.Rows[i]["FStockQty"].ToString() + ",FAuxCommitQty =  FAuxCommitQty + " + dtDtl.Rows[i]["FStockQty"].ToString() + ",FSecCommitQty =  FSecCommitQty + " + dtDtl.Rows[i]["FStockQty"].ToString() + " WHERE FInterID = " + dtDtl.Rows[i]["FSourceInterId"].ToString() + " AND FEntryID = " + dtDtl.Rows[i]["FSourceEntryID"].ToString() + ";";

                SqlOperation(0, strSQL);
            }
            #endregion

            if (conn.State == ConnectionState.Open)
                conn.Close();

            return "yes@ID:" + FInterID.ToString() + ";Number:" + FBillNo;
        }
        #endregion

        #region 调拨单
        /// <summary>
        /// 调拨单
        /// </summary>
        /// <param name="pHead">表头：FDeptID|FSManagerID|FFManagerID|FBillerID|FCheckerID</param>
        /// <param name="pDetails">表体：[FItemNumber|FSCStockNumber|FSCSPNumber|FBatchNo|FQty|FDCStockNumber|FDCSPNumber|FNote],......</param>
        /// <returns>yes@ID:xxxx;Number:xxxx/no@ExceptionMessage</returns>
        public static string ICStockBillForTrans(string pHead, string pDetails)
        {
            object obj;
            string strSQL;
            DataTable dtDtl, dtCheck;
            DataRow dr;
            SqlConnection conn;

            if (pHead.Trim() == string.Empty || pDetails.Trim() == string.Empty)
                return "no@x000:参数必填。";

            int FInterID;
            string FBillNo;

            GetICMaxIDAndBillNo(41, out FInterID, out FBillNo);

            if (FBillNo.IndexOf("Error") >= 0)
                return "no@x001:" + FBillNo;

            //定义表头字段
            int FDeptID, FSManagerID, FFManagerID, FBillerID, FCheckerID;

            //定义表体字段
            int FItemID, FUnitID, FSCStockID, FSCSPID, FDCStockID, FDCSPID;
            decimal FQty;
            string FItem, FSCStock, FSCSP, FDCStock, FDCSP, FBatchNo = string.Empty, FNote = string.Empty;

            #region 字段赋值
            try
            {
                FDeptID = int.Parse(pHead.Substring(0, pHead.IndexOf("|")));//FDeptID
                pHead = pHead.Substring(pHead.IndexOf("|") + 1);
                FSManagerID = int.Parse(pHead.Substring(0, pHead.IndexOf("|")));//FSManagerID
                pHead = pHead.Substring(pHead.IndexOf("|") + 1);
                FFManagerID = int.Parse(pHead.Substring(0, pHead.IndexOf("|")));//FFManagerID
                pHead = pHead.Substring(pHead.IndexOf("|") + 1);
                FBillerID = int.Parse(pHead.Substring(0, pHead.IndexOf("|")));//FBillerID
                pHead = pHead.Substring(pHead.IndexOf("|") + 1);
                FCheckerID = int.Parse(pHead.Substring(pHead.IndexOf("|") + 1));//FCheckerID

                //
                dtDtl = new DataTable();
                dtDtl.Columns.Add("FItemID");
                dtDtl.Columns.Add("FUnitID");
                dtDtl.Columns.Add("FSCStockID");
                dtDtl.Columns.Add("FSCSPID");
                dtDtl.Columns.Add("FBatchNo");

                dtDtl.Columns.Add("FQty");
                dtDtl.Columns.Add("FDCStockID");
                dtDtl.Columns.Add("FDCSPID");
                dtDtl.Columns.Add("FNote");

                string strTemp;
                do
                {
                    if (pDetails.IndexOf("],[") > 0)
                    {
                        strTemp = pDetails.Substring(0, pDetails.IndexOf("]") + 1);
                        pDetails = pDetails.Substring(pDetails.IndexOf("]") + 2);
                    }
                    else
                    {
                        strTemp = pDetails;
                        pDetails = "";
                    }

                    FItem = strTemp.Substring(1, strTemp.IndexOf("|") - 1);//FItemNumber
                    strTemp = strTemp.Substring(strTemp.IndexOf("|") + 1);

                    FSCStock = strTemp.Substring(0, strTemp.IndexOf("|"));//FSCStockNumber
                    strTemp = strTemp.Substring(strTemp.IndexOf("|") + 1);
                    FSCSP = strTemp.Substring(0, strTemp.IndexOf("|"));//FSCSPNumber
                    strTemp = strTemp.Substring(strTemp.IndexOf("|") + 1);
                    FBatchNo = strTemp.Substring(0, strTemp.IndexOf("|"));//FBatchNo
                    strTemp = strTemp.Substring(strTemp.IndexOf("|") + 1);
                    FQty = decimal.Parse(strTemp.Substring(0, strTemp.IndexOf("|")));//FQTY

                    strTemp = strTemp.Substring(strTemp.IndexOf("|") + 1);
                    FDCStock = strTemp.Substring(0, strTemp.IndexOf("|"));//FDCStockNumber
                    strTemp = strTemp.Substring(strTemp.IndexOf("|") + 1);
                    FDCSP = strTemp.Substring(0, strTemp.IndexOf("|"));//FDCSPNumber
                    strTemp = strTemp.Substring(strTemp.IndexOf("|") + 1);
                    FNote = strTemp.Substring(0, strTemp.IndexOf("]"));//FNote

                    //仓库检验
                    if (FSCStock == "" || FDCStock == "")
                        return "no@请输入仓库编码。";

                    if (FSCStock != FDCStock)
                    {
                        obj = SqlOperation(3, "SELECT FItemID,FNumber FROM t_Stock WHERE FNumber IN('" + FSCStock + "','" + FDCStock + "')");

                        if (obj == null || ((DataTable)obj).Rows.Count < 2)
                            return "no@未找到仓库信息[" + FSCStock + "," + FDCStock + "]";

                        if (((DataTable)obj).Rows[0]["FNumber"].ToString() == FSCStock)
                        {
                            FSCStockID = int.Parse(((DataTable)obj).Rows[0]["FItemID"].ToString());//FSCStockID
                            FDCStockID = int.Parse(((DataTable)obj).Rows[1]["FItemID"].ToString());//FDCStockID
                        }
                        else
                        {
                            FSCStockID = int.Parse(((DataTable)obj).Rows[1]["FItemID"].ToString());//FSCStockID
                            FDCStockID = int.Parse(((DataTable)obj).Rows[0]["FItemID"].ToString());//FDCStockID
                        }
                    }
                    else
                    {
                        obj = SqlOperation(1, "SELECT FItemID FROM t_Stock WHERE FNumber = '" + FSCStock + "'");

                        if (obj == null)
                            return "no@未找到仓库信息[" + FSCStock + "]";

                        FSCStockID = int.Parse(obj.ToString());
                        FDCStockID = FSCStockID;
                    }

                    //仓位检验
                    if (FSCSP == "" || FDCSP == "")
                        return "no@请输入仓位编码。";

                    if (FSCSP != FDCSP)
                    {
                        obj = SqlOperation(3, "SELECT FSPID,FNumber FROM t_StockPlace WHERE FNumber IN('" + FSCSP + "','" + FDCSP + "')");

                        if (obj == null || ((DataTable)obj).Rows.Count < 2)
                            return "no@未找到仓位信息[" + FSCSP + "," + FDCSP + "]";

                        if (((DataTable)obj).Rows[0]["FNumber"].ToString() == FSCSP)
                        {
                            FSCSPID = int.Parse(((DataTable)obj).Rows[0]["FSPID"].ToString());//FSCSPID
                            FDCSPID = int.Parse(((DataTable)obj).Rows[1]["FSPID"].ToString());//FDCSPID
                        }
                        else
                        {
                            FSCSPID = int.Parse(((DataTable)obj).Rows[1]["FSPID"].ToString());//FSCSPID
                            FDCSPID = int.Parse(((DataTable)obj).Rows[0]["FSPID"].ToString());//FDCSPID
                        }
                    }
                    else
                    {
                        obj = SqlOperation(1, "SELECT FSPID FROM t_StockPlace WHERE FNumber = '" + FSCSP + "'");

                        if (obj == null)
                            return "no@未找到仓位信息[" + FSCSP + "]";

                        FSCSPID = int.Parse(obj.ToString());
                        FDCSPID = FSCSPID;
                    }

                    //物料检验
                    if (FItem == "")
                        return "no@请输入物料编码";

                    strSQL = "SELECT MTL.FItemID,MTL.FUnitID,ISNULL(INV.FQty,0) FStockQty,CASE WHEN " + FQty.ToString() + @" > ISNULL(INV.FQty,0) THEN -1 ELSE 0 END Flag
                    FROM t_ICItem MTL
                    LEFT JOIN ICInventory INV ON MTL.FItemID = INV.FItemID AND INV.FBatchNo = '" + FBatchNo + "' AND INV.FStockID = " + FSCStockID.ToString() + " AND INV.FStockPlaceID = " + FSCSPID.ToString() + @"
                    WHERE MTL.FNumber = '" + FItem + "'";

                    obj = SqlOperation(3, strSQL);

                    if (obj == null || ((DataTable)obj).Rows.Count == 0)
                        return "no@未找到物料信息[" + FItem + "]";

                    FItemID = int.Parse(((DataTable)obj).Rows[0]["FItemID"].ToString());
                    FUnitID = int.Parse(((DataTable)obj).Rows[0]["FUnitID"].ToString());

                    if (int.Parse(((DataTable)obj).Rows[0]["Flag"].ToString()) == -1)
                    {
                        return "no@物料[" + FItem + "]调拨数量[" + FQty.ToString() + "]大于库存数量[" + decimal.Parse(((DataTable)obj).Rows[0]["FStockQty"].ToString()) + "]";
                    }

                    dr = dtDtl.NewRow();

                    dr["FItemID"] = FItemID;
                    dr["FUnitID"] = FUnitID;
                    dr["FSCStockID"] = FSCStockID;
                    dr["FSCSPID"] = FSCSPID;
                    dr["FBatchNo"] = FBatchNo;

                    dr["FQty"] = FQty;
                    dr["FDCStockID"] = FDCStockID;
                    dr["FDCSPID"] = FDCSPID;
                    dr["FNote"] = FNote;

                    dtDtl.Rows.Add(dr);
                }
                while (pDetails.Length > 0);
            }
            catch (Exception ex)
            {
                return "no@x002:" + ex.Message;
            }
            #endregion

            #region 判断 是否有本次调拨总数量大于即时库存数量
            dtCheck = dtDtl.Clone();//获取汇总FQty的物料信息
            dtCheck.ImportRow(dtDtl.Rows[0]);

            for (int i = 1; i < dtDtl.Rows.Count; i++)
            {
                for (int j = 0; j < dtCheck.Rows.Count; j++)
                {
                    if (dtCheck.Rows[j]["FItemID"] == dtDtl.Rows[i]["FItemID"] && dtCheck.Rows[j]["FBatchNo"] == dtDtl.Rows[i]["FBatchNo"] && dtCheck.Rows[j]["FSCStockID"] == dtDtl.Rows[i]["FSCStockID"] && dtCheck.Rows[j]["FSCSPID"] == dtDtl.Rows[i]["FSCSPID"])
                    {
                        dtCheck.Rows[j]["FQty"] = decimal.Parse(dtDtl.Rows[i]["FQty"].ToString()) + decimal.Parse(dtCheck.Rows[j]["FQty"].ToString());
                        break;
                    }

                    if (j == dtCheck.Rows.Count - 1)//未匹配到数据
                    {
                        dtCheck.ImportRow(dtDtl.Rows[i]);
                        break;
                    }
                }
            }
            //物料总调拨数量与库存数量对比
            for (int i = 0; i < dtCheck.Rows.Count; i++)
            {
                if (dtCheck.Rows[i]["FSCSPID"].ToString() != "0")
                    strSQL = @"SELECT MTL.FNumber,CASE WHEN " + dtCheck.Rows[i]["FQty"].ToString() + @" > ISNULL(INV.FQty,0) THEN ISNULL(INV.FQty,0) ELSE -1 END FQty
                    FROM t_ICItem MTL
                    LEFT JOIN ICInventory INV ON INV.FItemID = MTL.FItemID AND INV.FStockID = " + dtCheck.Rows[i]["FSCStockID"].ToString() + " AND INV.FBatchNo = '" + dtCheck.Rows[i]["FBatchNo"].ToString() + "' AND FStockPlaceID = " + dtCheck.Rows[i]["FSCSPID"].ToString() + @"
                    WHERE MTL.FItemID = " + dtCheck.Rows[i]["FItemID"].ToString();
                else
                    strSQL = @"SELECT MTL.FNumber,CASE WHEN " + dtCheck.Rows[i]["FQty"].ToString() + @" > ISNULL(INV.FQty,0) THEN ISNULL(INV.FQty,0) ELSE -1 END FQty
                    FROM t_ICItem MTL
                    LEFT JOIN ICInventory INV ON INV.FItemID = MTL.FItemID AND INV.FStockID = " + dtCheck.Rows[i]["FSCStockID"].ToString() + " AND INV.FBatchNo = '" + dtCheck.Rows[i]["FBatchNo"].ToString() + @"'
                    WHERE MTL.FItemID = " + dtCheck.Rows[i]["FItemID"].ToString();

                obj = SqlOperation(3, strSQL);

                if (obj == null || ((DataTable)obj).Rows.Count == 0)
                    return "no@物料输入有误";
                else if (decimal.Parse(((DataTable)obj).Rows[0]["FQty"].ToString()) != -1)
                    return "no@[" + ((DataTable)obj).Rows[0]["FNumber"].ToString() + "]物料本次调拨总数量[" + dtCheck.Rows[i]["FQty"].ToString() + "]大于库存数量[" + ((DataTable)obj).Rows[0]["FQty"].ToString() + "]";
            }
            #endregion

            conn = new SqlConnection(C_CONNECTIONSTRING);

            #region 插入表头
            try
            {
                conn.Open();
                SqlCommand cmdH = conn.CreateCommand();
                cmdH.CommandType = CommandType.Text;

                cmdH.Parameters.Add("@FInterID", SqlDbType.Int);
                cmdH.Parameters.Add("@FBillNo", SqlDbType.NVarChar, 255);

                cmdH.Parameters.Add("@FDeptID", SqlDbType.Int);
                cmdH.Parameters.Add("@FSManagerID", SqlDbType.Int);
                cmdH.Parameters.Add("@FFManagerID", SqlDbType.Int);
                cmdH.Parameters.Add("@FBillerID", SqlDbType.Int);
                cmdH.Parameters.Add("@FCheckerID", SqlDbType.Int);

                cmdH.Parameters["@FInterID"].Value = FInterID;
                cmdH.Parameters["@FBillNo"].Value = FBillNo;

                cmdH.Parameters["@FDeptID"].Value = FDeptID;
                cmdH.Parameters["@FSManagerID"].Value = FSManagerID;
                cmdH.Parameters["@FFManagerID"].Value = FFManagerID;
                cmdH.Parameters["@FBillerID"].Value = FBillerID;
                cmdH.Parameters["@FCheckerID"].Value = FCheckerID;

                strSQL = @"INSERT INTO dbo.ICStockBill(FInterID,FBillNo,FBrNo,FTranType,FROB,FDate,FDeptID,FPurposeID,FSManagerID,FFManagerID,  FBillerID,FCheckerID,FCheckDate,FStatus,FRefType,FMarketingStyle) 
                VALUES (@FInterID,@FBillNo,'0',41,1,CONVERT(VARCHAR(10),GETDATE(),120),@FDeptID,0,@FSManagerID,@FFManagerID,   @FBillerID,@FCheckerID,GETDATE(),1,12561,12530)";

                cmdH.CommandText = strSQL;
                cmdH.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                return "no@x003:" + ex.Message;
            }
            finally
            {
                conn.Close();
            }
            #endregion

            #region 插入表体
            conn.Open();
            SqlCommand cmdD = conn.CreateCommand();
            cmdD.CommandType = CommandType.Text;

            cmdD.Parameters.Add("@FInterID", SqlDbType.Int);
            cmdD.Parameters.Add("@FEntryID", SqlDbType.Int);

            cmdD.Parameters.Add("@FItemID", SqlDbType.Int);
            cmdD.Parameters.Add("@FUnitID", SqlDbType.Int);
            cmdD.Parameters.Add("@FSCStockID", SqlDbType.Int);
            cmdD.Parameters.Add("@FSCSPID", SqlDbType.Int);
            cmdD.Parameters.Add("@FBatchNo", SqlDbType.VarChar);

            cmdD.Parameters.Add("@FQty", SqlDbType.Decimal);
            cmdD.Parameters.Add("@FDCStockID", SqlDbType.Int);
            cmdD.Parameters.Add("@FDCSPID", SqlDbType.Int);
            cmdD.Parameters.Add("@FNote", SqlDbType.VarChar);

            for (int i = 0; i < dtDtl.Rows.Count; i++)
            {
                try
                {
                    cmdD.Parameters["@FInterID"].Value = FInterID;
                    cmdD.Parameters["@FEntryID"].Value = i + 1;

                    cmdD.Parameters["@FItemID"].Value = dtDtl.Rows[i]["FItemID"].ToString();
                    cmdD.Parameters["@FUnitID"].Value = dtDtl.Rows[i]["FUnitID"].ToString();
                    cmdD.Parameters["@FSCStockID"].Value = dtDtl.Rows[i]["FSCStockID"].ToString();
                    cmdD.Parameters["@FSCSPID"].Value = dtDtl.Rows[i]["FSCSPID"].ToString();
                    cmdD.Parameters["@FBatchNo"].Value = dtDtl.Rows[i]["FBatchNo"].ToString();

                    cmdD.Parameters["@FQty"].Value = dtDtl.Rows[i]["FQty"].ToString();
                    cmdD.Parameters["@FDCStockID"].Value = dtDtl.Rows[i]["FDCStockID"].ToString();
                    cmdD.Parameters["@FDCSPID"].Value = dtDtl.Rows[i]["FDCSPID"].ToString();
                    cmdD.Parameters["@FNote"].Value = dtDtl.Rows[i]["FNote"].ToString();

                    strSQL = @"INSERT INTO dbo.ICstockbillentry(FInterID,FEntryID,FBrNo,FItemID,FBatchNo,FQty,FQtyMust,FUnitID,FAuxQtyMust,FAuxQty, FSCStockID,FSCSPID,FDCStockID,FDCSPID,FChkPassItem,FNote,FPlanMode)
                    VALUES(@FInterID,@FEntryID,'0',@FItemID,@FBatchNo,@FQty,@FQty,@FUnitID,@FQty,@FQty, @FSCStockID,@FSCSPID,@FDCStockID,@FDCSPID,1058,@FNote,14036)";

                    cmdD.CommandText = strSQL;
                    cmdD.ExecuteNonQuery();
                }
                catch (Exception ex)
                {
                    //删除已生成的单据数据
                    SqlOperation(0, "DELETE FROM ICStockBill WHERE FInterID = " + FInterID.ToString() + " DELETE FROM ICstockbillentry WHERE FInterID = " + FInterID.ToString());

                    conn.Close();
                    return "no@x004:" + ex.Message;
                }
            }
            #endregion

            #region 反写库存
            for (int i = 0; i < dtDtl.Rows.Count; i++)
            {
                if (dtDtl.Rows[i]["FDCStockID"] == dtDtl.Rows[i]["FSCStockID"] && dtDtl.Rows[i]["FDCSPID"] == dtDtl.Rows[i]["FSCSPID"])
                    continue;

                strSQL = @"
                --源仓扣库存
                UPDATE ICInventory SET FQty = FQty - " + dtDtl.Rows[i]["FQty"].ToString() + " WHERE FItemID = " + dtDtl.Rows[i]["FItemID"].ToString() + " AND FBatchNo = '" + dtDtl.Rows[i]["FBatchNo"].ToString() + "' AND FStockID = " + dtDtl.Rows[i]["FSCStockID"].ToString() + " AND FStockPlaceID = " + dtDtl.Rows[i]["FSCSPID"].ToString() + @";
                --目标仓加库存
                MERGE INTO ICInventory AS IC
                USING
                (
                    SELECT " + dtDtl.Rows[i]["FItemID"].ToString() + " FItemID," + dtDtl.Rows[i]["FQty"].ToString() + " FQty,'" + dtDtl.Rows[i]["FBatchNo"].ToString() + "' FBatchNo, " + dtDtl.Rows[i]["FDCStockID"].ToString() + " FStockID, " + dtDtl.Rows[i]["FDCSPID"].ToString() + @" FSPID
                ) AS DT ON IC.FItemID = DT.FItemID AND IC.FStockID = DT.FStockID AND IC.FBatchNo = DT.FBatchNo AND IC.FStockPlaceID = DT.FSPID
                WHEN MATCHED
                    THEN UPDATE SET FQty = IC.FQty + DT.FQty
                WHEN NOT MATCHED
                    THEN INSERT(FBrNo,FItemID,FStockID,FStockPlaceID,FQty,FBatchNo) VALUES(0,DT.FItemID,DT.FStockID,DT.FSPID,DT.FQty,DT.FBatchNo);";

                SqlOperation(0, strSQL);
            }
            #endregion

            if (conn.State == ConnectionState.Open)
                conn.Close();

            return "yes@ID:" + FInterID.ToString() + ";Number:" + FBillNo;
        }
        #endregion

        #region 销售出库单
        /// <summary>
        /// 销售出库单
        /// </summary>
        /// <param name="pHead">表头：FDeptID|FSManagerID|FFManagerID|FBillerID|FSourceBillNo|FNote</param>
        /// <param name="pDetails">表体：[FItemNumber|FDCStockNumber|FDCSPNumber|FBatchNo|FQty|FSourceBillNo|FNote],......</param>
        /// <returns>yes@ID:xxxx;Number:xxxx/no@ExceptionMessage</returns>
        public static string ICStockBillForXOut(string pHead, string pDetails)
        {
            object obj;
            string strSQL;
            DataTable dtDtl, dtCheck;
            DataRow dr;
            SqlConnection conn;

            if (pHead.Trim() == string.Empty || pDetails.Trim() == string.Empty)
                return "no@x000:参数必填。";

            int FInterID;
            string FBillNo;

            GetICMaxIDAndBillNo(21, out FInterID, out FBillNo);

            if (FBillNo.IndexOf("Error") >= 0)
                return "no@x001:" + FBillNo;

            conn = new SqlConnection(C_CONNECTIONSTRING);

            //销售订单：SEOrder
            string SEOrderBillNo;
            int SEOrderInterID, SEOrderEntryID;

            //发货通知单：SEOutStock
            int FOrgBillInterID, FSEOutStockInterID, FSEOutStockEntryID, FCustID;

            //定义表头字段
            string FNote, FSEOutStockBillNo;
            int FDeptID, FSManagerID, FFManagerID, FBillerID;

            //定义表体字段
            int FItemID, FUnitID, FDCStockID, FDCSPID;
            decimal FPrice, FQty, CanOutQTY;
            string FNoteD, FItem, FSCStock, FSCSP, FBatchNo, FSourceBillNo;

            //物料
            bool FBatchManager = false;

            #region 字段赋值
            try
            {
                //解释表头字段
                FDeptID = int.Parse(pHead.Substring(0, pHead.IndexOf("|")));//FDeptID
                pHead = pHead.Substring(pHead.IndexOf("|") + 1);
                FSManagerID = int.Parse(pHead.Substring(0, pHead.IndexOf("|")));//FSManagerID
                pHead = pHead.Substring(pHead.IndexOf("|") + 1);
                FFManagerID = int.Parse(pHead.Substring(0, pHead.IndexOf("|")));//FFManagerID
                pHead = pHead.Substring(pHead.IndexOf("|") + 1);
                FBillerID = int.Parse(pHead.Substring(0, pHead.IndexOf("|")));//FBillerID
                pHead = pHead.Substring(pHead.IndexOf("|") + 1);
                FSEOutStockBillNo = pHead.Substring(0, pHead.IndexOf("|"));//SEOutStockBillNo
                //
                FNote = pHead.Substring(pHead.IndexOf("|") + 1);//FNote
                
                obj = SqlOperation(3, "SELECT FInterID,FClosed,FCustID FROM SEOutStock WHERE FBillNo = '" + FSEOutStockBillNo + "'");
                if (obj == null || ((DataTable)obj).Rows.Count == 0)
                    return "no@没有此单据数据[" + FSEOutStockBillNo + "]";

                //FConsignee = ((DataTable)obj).Rows[0]["FConsignee"].ToString();//收货方
                FOrgBillInterID = int.Parse(((DataTable)obj).Rows[0]["FInterID"].ToString());
                FCustID = int.Parse(((DataTable)obj).Rows[0]["FCustID"].ToString());

                //源单关闭、审核和作废状态的判断-未做判断

                //解释表体字段
                dtDtl = new DataTable();
                dtDtl.Columns.Add("FItem");

                dtDtl.Columns.Add("FItemID");
                dtDtl.Columns.Add("FUnitID");
                dtDtl.Columns.Add("FDCStockID");
                dtDtl.Columns.Add("FDCSPID");
                dtDtl.Columns.Add("FBatchNo");

                dtDtl.Columns.Add("FQty");
                dtDtl.Columns.Add("FPrice");
                dtDtl.Columns.Add("FAmount");
                dtDtl.Columns.Add("FSourceBillNo");
                dtDtl.Columns.Add("FInterID");

                dtDtl.Columns.Add("FEntryID");
                dtDtl.Columns.Add("FNote");
                dtDtl.Columns.Add("CanOutQTY");

                dtDtl.Columns.Add("SEOrderBillNo");
                dtDtl.Columns.Add("SEOrderInterID");
                dtDtl.Columns.Add("SEOrderEntryID");

                string strTemp;
                do
                {
                    if (pDetails.IndexOf("],[") > 0)
                    {
                        strTemp = pDetails.Substring(0, pDetails.IndexOf("]") + 1);
                        pDetails = pDetails.Substring(pDetails.IndexOf("]") + 2);
                    }
                    else
                    {
                        strTemp = pDetails;
                        pDetails = "";
                    }

                    FItem = strTemp.Substring(1, strTemp.IndexOf("|") - 1);//MTLNumber
                    strTemp = strTemp.Substring(strTemp.IndexOf("|") + 1);
                    FSCStock = strTemp.Substring(0, strTemp.IndexOf("|"));//FStockNumber
                    strTemp = strTemp.Substring(strTemp.IndexOf("|") + 1);
                    FSCSP = strTemp.Substring(0, strTemp.IndexOf("|"));//FSPNumber
                    strTemp = strTemp.Substring(strTemp.IndexOf("|") + 1);
                    FBatchNo = strTemp.Substring(0, strTemp.IndexOf("|"));//FBatchNo
                    strTemp = strTemp.Substring(strTemp.IndexOf("|") + 1);
                    FQty = decimal.Parse(strTemp.Substring(0, strTemp.IndexOf("|")));//FQTY

                    strTemp = strTemp.Substring(strTemp.IndexOf("|") + 1);
                    FSourceBillNo = strTemp.Substring(0, strTemp.IndexOf("|"));//FSourceBillNo
                    //
                    //FNoteD = strTemp.Substring(strTemp.IndexOf("|") + 1, strTemp.Length - strTemp.IndexOf("|") - 2);//FNoteD
                    strTemp = strTemp.Substring(strTemp.IndexOf("|") + 1);
                    FNoteD = strTemp.Substring(0, strTemp.IndexOf("]"));//FNoteD

                    strSQL = @"SELECT A.FInterID,AE.FEntryID,AE.FItemID,MTL.FUnitID,AE.FQty,AE.FStockQty,AE.FQty - AE.FStockQty CanOutQTY,AE.FPrice,MTL.FBatchManager,ISNULL(B.FBillNo,'') SEOrderBillNo,ISNULL(B.FInterID,0) SEOrderInterID,ISNULL(BE.FEntryId,0) SEOrderEntryID
                    FROM SEOutStock A
                    INNER JOIN SEOutStockEntry AE ON A.FInterID = AE.FInterID
                    INNER JOIN t_ICItem MTL ON AE.FItemID = MTL.FItemID
                    LEFT JOIN SEOrder B ON AE.FOrderBillNo = B.FBillNo
                    LEFT JOIN SEOrderEntry BE ON B.FInterID = BE.FInterID AND AE.FOrderEntryID = BE.FEntryID
                    WHERE A.FBillNo = '" + FSourceBillNo + "' AND MTL.FNumber = '" + FItem + "'";

                    obj = SqlOperation(3, strSQL);
                    if (obj == null || ((DataTable)obj).Rows.Count == 0)
                        //return "no@未找到源单对应的物料信息";
                        return "no@未找到物料信息[" + FSourceBillNo + "].[" + FItem + "]";

                    FSEOutStockInterID = int.Parse(((DataTable)obj).Rows[0]["FInterID"].ToString());//FInterID
                    FSEOutStockEntryID = int.Parse(((DataTable)obj).Rows[0]["FEntryID"].ToString());//FEntryID
                    FItemID = int.Parse(((DataTable)obj).Rows[0]["FItemID"].ToString());//MTLID
                    FUnitID = int.Parse(((DataTable)obj).Rows[0]["FUnitID"].ToString());//UnitID
                    FPrice = decimal.Parse(((DataTable)obj).Rows[0]["FPrice"].ToString());//Price
                    CanOutQTY = decimal.Parse(((DataTable)obj).Rows[0]["CanOutQTY"].ToString());//CanOutQTY

                    FBatchManager = ((DataTable)obj).Rows[0]["FBatchManager"].ToString() == "0" ? false : true;//是否采用业务批次管理

                    SEOrderBillNo = ((DataTable)obj).Rows[0]["SEOrderBillNo"].ToString();
                    SEOrderInterID= int.Parse(((DataTable)obj).Rows[0]["SEOrderInterID"].ToString());
                    SEOrderEntryID= int.Parse(((DataTable)obj).Rows[0]["SEOrderEntryID"].ToString());

                    if (FQty > CanOutQTY)
                    {
                        return "no@销售订单[" + FSourceBillNo + "],产品[" + FItem + "]的可出数量[" + CanOutQTY.ToString() + "]小于出库数量[" + FQty.ToString() + "]请核实。";
                    }

                    if (FSCStock == "")
                        FDCStockID = 0;
                    else
                    {
                        obj = SqlOperation(3, "SELECT FItemID FROM t_Stock WHERE FNumber = '" + FSCStock + "'");
                        if (obj == null || ((DataTable)obj).Rows.Count == 0)
                            return "no@未找到仓库信息[" + FSCStock + "]";
                        FDCStockID = int.Parse(((DataTable)obj).Rows[0]["FItemID"].ToString());//FDCStockID
                    }

                    if (FSCSP == "")
                        FDCSPID = 0;
                    else
                    {
                        obj = SqlOperation(3, "SELECT FSPID FROM t_StockPlace WHERE FNumber = '" + FSCSP + "'");
                        if (obj == null || ((DataTable)obj).Rows.Count == 0)
                            return "no@未找到仓位信息[" + FSCSP + "]";
                        FDCSPID = int.Parse(((DataTable)obj).Rows[0]["FSPID"].ToString());//FDCSPID
                    }

                    if (string.IsNullOrEmpty(FBatchNo) && FBatchManager)
                    {
                        return "no@[" + FItem + "]物料已经启用批次号管理，请携带批次号。";
                    }

                    dr = dtDtl.NewRow();
                    dr["FItem"] = FItem;

                    dr["FItemID"] = FItemID;
                    dr["FUnitID"] = FUnitID;
                    dr["FDCStockID"] = FDCStockID;
                    dr["FDCSPID"] = FDCSPID;
                    dr["FBatchNo"] = FBatchNo;

                    dr["FQty"] = FQty;
                    dr["FPrice"] = FPrice;
                    dr["FAmount"] = FQty * FPrice;
                    dr["FSourceBillNo"] = FSourceBillNo;
                    dr["FInterID"] = FSEOutStockInterID;

                    dr["FEntryID"] = FSEOutStockEntryID;
                    dr["FNote"] = FNoteD;
                    dr["CanOutQTY"] = CanOutQTY;

                    dr["SEOrderBillNo"] = SEOrderBillNo;
                    dr["SEOrderInterID"] = SEOrderInterID;
                    dr["SEOrderEntryID"] = SEOrderEntryID;

                    dtDtl.Rows.Add(dr);
                }
                while (pDetails.Length > 0);
            }
            catch (Exception ex)
            {
                return "no@" + ex.Message;
            }
            #endregion

            #region 对入库数量的判断
            dtCheck = dtDtl.Clone();//获取汇总FQty的物料信息
            dtCheck.ImportRow(dtDtl.Rows[0]);//把第一条数据写入dtPO中
            //汇总入库情况
            for (int i = 1; i < dtDtl.Rows.Count; i++)
            {
                for (int j = 0; j < dtCheck.Rows.Count; j++)//根据采购单号与物料编码进行匹配
                {
                    if (dtCheck.Rows[j]["FSourceBillNo"].ToString() == dtDtl.Rows[i]["FSourceBillNo"].ToString() && dtCheck.Rows[j]["FItem"].ToString() == dtDtl.Rows[i]["FItem"].ToString())
                    {
                        dtCheck.Rows[j]["FQty"] = decimal.Parse(dtCheck.Rows[j]["FQty"].ToString()) + decimal.Parse(dtDtl.Rows[i]["FQty"].ToString());
                        break;
                    }

                    if (j == dtCheck.Rows.Count - 1)//未匹配到数据
                    {
                        dtCheck.ImportRow(dtDtl.Rows[i]);
                        break;
                    }
                }
            }

            for (int i = 0; i < dtCheck.Rows.Count; i++)
            {
                if (decimal.Parse(dtCheck.Rows[i]["FQty"].ToString()) > decimal.Parse(dtCheck.Rows[i]["CanOutQTY"].ToString()))//入库数量大于采购数量
                {
                    return "no@[" + dtCheck.Rows[i]["FSourceBillNo"].ToString() + "].[" + dtCheck.Rows[i]["FItem"].ToString() + "]物料本次总入库数量[" + dtCheck.Rows[i]["FQty"].ToString() + "]大于未入库数量[" + dtCheck.Rows[i]["CanOutQTY"].ToString() + "]。";
                }
            }
            #endregion

            #region 对即时库存的判断

            for (int i = 0; i < dtDtl.Rows.Count; i++)
            {
                if (dtDtl.Rows[i]["FDCSPID"] == null || dtDtl.Rows[i]["FDCSPID"].ToString() == "0")
                    strSQL = "SELECT FQty FROM ICInventory WHERE FItemID = " + dtDtl.Rows[i]["FItemID"].ToString() + " AND FStockID = " + dtDtl.Rows[i]["FDCStockID"].ToString() + " AND FBatchNo = '" + dtDtl.Rows[i]["FBatchNo"].ToString() + "'";
                else
                    strSQL = "SELECT FQty FROM ICInventory WHERE FItemID = " + dtDtl.Rows[i]["FItemID"].ToString() + " AND FStockID = " + dtDtl.Rows[i]["FDCStockID"].ToString() + " AND FStockPlaceID = " + dtDtl.Rows[i]["FDCSPID"].ToString() + " AND FBatchNo = '" + dtDtl.Rows[i]["FBatchNo"].ToString() + "'";

                obj = SqlOperation(1, strSQL);

                if (obj == null)
                    return "no@" + "物料[" + dtDtl.Rows[i]["FItem"].ToString() + "]未能匹配到即时库存";

                if ((decimal)obj < decimal.Parse(dtDtl.Rows[i]["FQty"].ToString()))
                    return "no@" + "物料[" + dtDtl.Rows[i]["FItem"].ToString() + "]即时库存[" + ((decimal)obj).ToString() + "]小于出库数量[" + dtDtl.Rows[i]["FQty"].ToString() + "]";
            }
            #endregion

            #region 插入主表
            try
            {
                conn.Open();
                SqlCommand cmdH = conn.CreateCommand();
                cmdH.CommandType = CommandType.Text;

                cmdH.Parameters.Add("@FInterID", SqlDbType.Int);
                cmdH.Parameters.Add("@FBillNo", SqlDbType.VarChar, 255);
                cmdH.Parameters.Add("@FNote", SqlDbType.VarChar, 255);
                cmdH.Parameters.Add("@FDeptID", SqlDbType.Int);
                //cmdH.Parameters.Add("@FConsignee", SqlDbType.VarChar);

                cmdH.Parameters.Add("@FSManagerID", SqlDbType.Int);
                cmdH.Parameters.Add("@FFManagerID", SqlDbType.Int);
                cmdH.Parameters.Add("@FBillerID", SqlDbType.Int);
                cmdH.Parameters.Add("@FOrgBillInterID", SqlDbType.Int);
                cmdH.Parameters.Add("@FCustID", SqlDbType.Int);

                cmdH.Parameters["@FInterID"].Value = FInterID;
                cmdH.Parameters["@FBillNo"].Value = FBillNo;
                cmdH.Parameters["@FNote"].Value = FNote;
                cmdH.Parameters["@FDeptID"].Value = FDeptID;
                //cmdH.Parameters["@FConsignee"].Value = FConsignee;

                cmdH.Parameters["@FSManagerID"].Value = FSManagerID;
                cmdH.Parameters["@FFManagerID"].Value = FFManagerID;
                cmdH.Parameters["@FBillerID"].Value = FBillerID;
                cmdH.Parameters["@FOrgBillInterID"].Value = FOrgBillInterID;
                cmdH.Parameters["@FCustID"].Value = FCustID;

                strSQL = @"INSERT INTO dbo.ICStockBill(FInterID,FBillNo,FBrNo,FTranType,FROB,Fdate,FNote,FDeptID,   FSManagerID,FFManagerID,FBillerID,FSupplyID,FSelTranType,FSaleStyle,FOrgBillInterID,FStatus,FCheckerID,FCheckDate)
                VALUES (@FInterID,@FBillNo,'0',21,1,CONVERT(VARCHAR(10),GETDATE(),120),@FNote,@FDeptID, @FSManagerID,@FFManagerID,@FBillerID,@FCustID,83,102,@FOrgBillInterID,1,@FBillerID,GETDATE())";

                cmdH.CommandText = strSQL;
                cmdH.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                return "no@" + ex.Message;
            }
            finally
            {
                conn.Close();
            }
            #endregion

            #region 插入表体
            conn.Open();
            SqlCommand cmdD = conn.CreateCommand();
            cmdD.CommandType = CommandType.Text;

            cmdD.Parameters.Add("@FInterID", SqlDbType.Int);
            cmdD.Parameters.Add("@FEntryID", SqlDbType.Int);
            cmdD.Parameters.Add("@FItemID", SqlDbType.Int);
            cmdD.Parameters.Add("@FBatchNo", SqlDbType.VarChar);
            cmdD.Parameters.Add("@FQty", SqlDbType.Decimal);

            cmdD.Parameters.Add("@FUnitID", SqlDbType.Int);
            cmdD.Parameters.Add("@FDCStockID", SqlDbType.Int);
            cmdD.Parameters.Add("@FDCSPID", SqlDbType.Int);
            cmdD.Parameters.Add("@FSourceBillNo", SqlDbType.VarChar, 50);
            cmdD.Parameters.Add("@FSourceInterId", SqlDbType.Int, 50);

            cmdD.Parameters.Add("@FSourceEntryID", SqlDbType.Int, 50);
            cmdD.Parameters.Add("@FPrice", SqlDbType.Decimal);
            cmdD.Parameters.Add("@FAmount", SqlDbType.Decimal);
            cmdD.Parameters.Add("@FNote", SqlDbType.VarChar);

            cmdD.Parameters.Add("@SEOrderBillNo", SqlDbType.VarChar);
            cmdD.Parameters.Add("@SEOrderInterID", SqlDbType.Int);
            cmdD.Parameters.Add("@SEOrderEntryID", SqlDbType.Int);

            for (int i = 0; i < dtDtl.Rows.Count; i++)
            {
                try
                {
                    cmdD.Parameters["@FInterID"].Value = FInterID;
                    cmdD.Parameters["@FEntryID"].Value = i + 1;
                    cmdD.Parameters["@FItemID"].Value = dtDtl.Rows[i]["FItemID"].ToString();
                    cmdD.Parameters["@FBatchNo"].Value = dtDtl.Rows[i]["FBatchNo"].ToString();
                    cmdD.Parameters["@FQty"].Value = dtDtl.Rows[i]["FQty"].ToString();

                    cmdD.Parameters["@FUnitID"].Value = dtDtl.Rows[i]["FUnitID"].ToString();
                    cmdD.Parameters["@FDCStockID"].Value = dtDtl.Rows[i]["FDCStockID"].ToString();
                    cmdD.Parameters["@FDCSPID"].Value = dtDtl.Rows[i]["FDCSPID"].ToString();
                    cmdD.Parameters["@FSourceBillNo"].Value = dtDtl.Rows[i]["FSourceBillNo"].ToString();
                    cmdD.Parameters["@FSourceInterId"].Value = dtDtl.Rows[i]["FInterID"].ToString();

                    cmdD.Parameters["@FSourceEntryID"].Value = dtDtl.Rows[i]["FEntryID"].ToString();
                    cmdD.Parameters["@FPrice"].Value = dtDtl.Rows[i]["FPrice"].ToString();
                    cmdD.Parameters["@FAmount"].Value = dtDtl.Rows[i]["FAmount"].ToString();
                    cmdD.Parameters["@FNote"].Value = dtDtl.Rows[i]["FNote"].ToString();

                    cmdD.Parameters["@SEOrderBillNo"].Value = dtDtl.Rows[i]["SEOrderBillNo"].ToString();
                    cmdD.Parameters["@SEOrderInterID"].Value = dtDtl.Rows[i]["SEOrderInterID"].ToString();
                    cmdD.Parameters["@SEOrderEntryID"].Value = dtDtl.Rows[i]["SEOrderEntryID"].ToString();

                    strSQL = @"INSERT INTO dbo.ICstockbillEntry(FInterID,FEntryID,FBrNo,FItemID,FBatchNo,FUnitID,FDCStockID,FDCSPID,FQty,FQtyMust,FAuxQty,FOutCommitQty,FOutSecCommitQty,FPrice,FAuxprice,FAmount,Fconsignprice,FconsignAmount,FSCBillNo,FSCBillInterID,FSourceBillNo,    FSourceInterId,FSourceEntryID,FSourceTranType,FChkPassItem,FNote,   FOrderBillNo,FOrderInterID,FOrderEntryID,FSEOutBillNo,FSEOutInterID,FSEOutEntryID)
                    VALUES(@FInterID,@FEntryID,'0',@FItemID,@FBatchNo,@FUnitID,@FDCStockID,@FDCSPID,@FQty,@FQty,@FQty,@FQty,@FQty,@FPrice,@FPrice,@FAmount,@FPrice,@FAmount,@FSourceBillNo,@FSourceInterId,@FSourceBillNo,    @FSourceInterId,@FSourceEntryID,83,1058,@FNote, @SEOrderBillNo,@SEOrderInterID,@SEOrderEntryID,@FSourceBillNo,@FSourceInterId,@FSourceEntryID)";

                    cmdD.CommandText = strSQL;
                    cmdD.ExecuteNonQuery();
                }
                catch (Exception ex)
                {
                    SqlOperation(0, "DELETE FROM ICStockBill WHERE FInterID = " + FInterID.ToString() + " DELETE FROM ICstockbillentry WHERE FInterID = " + FInterID.ToString());

                    conn.Close();
                    return "no@" + ex.Message;
                }
            }
            #endregion

            #region 反写库存和发货通知单

            for (int i = 0; i < dtDtl.Rows.Count; i++)
            {
                if (dtDtl.Rows[i]["FDCSPID"] == null || dtDtl.Rows[i]["FDCSPID"].ToString() == "0")
                    strSQL = @"MERGE INTO ICInventory AS IC
                    USING
                    (
	                    SELECT " + dtDtl.Rows[i]["FItemID"].ToString() + " FItemID, " + dtDtl.Rows[i]["FDCStockID"].ToString() + " FStockID," + dtDtl.Rows[i]["FDCSPID"].ToString() + " FSPID," + dtDtl.Rows[i]["FQty"].ToString() + " FQty,'" + dtDtl.Rows[i]["FBatchNo"].ToString() + @"' FBatchNo
                    ) AS DT ON IC.FItemID = DT.FItemID AND IC.FStockID = DT.FStockID AND IC.FBatchNo = DT.FBatchNo
                    WHEN MATCHED
                        THEN UPDATE SET FQty = IC.FQty - DT.FQty
                    WHEN NOT MATCHED
                        THEN INSERT(FBrNo,FItemID,FStockID,FQty) VALUES(0,DT.FItemID,DT.FStockID,DT.FQty);
                    UPDATE SEOutStockEntry SET FStockQty =  FStockQty + " + dtDtl.Rows[i]["FQty"].ToString() + ",FAuxStockQty = FAuxStockQty + " + dtDtl.Rows[i]["FQty"].ToString() + ",FAuxCommitQty = FAuxCommitQty +" + dtDtl.Rows[i]["FQty"].ToString() + " WHERE FInterID = " + dtDtl.Rows[i]["FInterID"].ToString() + " AND FEntryID = " + dtDtl.Rows[i]["FEntryID"].ToString() + @";
                    UPDATE SEOrderEntry SET FStockQty =  FStockQty + " + dtDtl.Rows[i]["FQty"].ToString() + ",FAuxStockQty = FAuxStockQty + " + dtDtl.Rows[i]["FQty"].ToString() + ",FAuxCommitQty = FAuxCommitQty +" + dtDtl.Rows[i]["FQty"].ToString() + " WHERE FInterID = " + dtDtl.Rows[i]["SEOrderInterID"].ToString() + " AND FEntryID = " + dtDtl.Rows[i]["SEOrderEntryID"].ToString() + ";";
                else
                    strSQL = @"MERGE INTO ICInventory AS IC
                    USING
                    (
	                    SELECT " + dtDtl.Rows[i]["FItemID"].ToString() + " FItemID, " + dtDtl.Rows[i]["FDCStockID"].ToString() + " FStockID," + dtDtl.Rows[i]["FDCSPID"].ToString() + " FSPID," + dtDtl.Rows[i]["FQty"].ToString() + " FQty,'" + dtDtl.Rows[i]["FBatchNo"].ToString() + @"' FBatchNo
                    ) AS DT ON IC.FItemID = DT.FItemID AND IC.FStockID = DT.FStockID AND IC.FBatchNo = DT.FBatchNo AND IC.FStockPlaceID = DT.FSPID
                    WHEN MATCHED
                        THEN UPDATE SET FQty = IC.FQty - DT.FQty
                    WHEN NOT MATCHED
                        THEN INSERT(FBrNo,FItemID,FStockID,FQty) VALUES(0,DT.FItemID,DT.FStockID,-DT.FQty);
                    UPDATE SEOutStockEntry SET FStockQty =  FStockQty + " + dtDtl.Rows[i]["FQty"].ToString() + ",FAuxStockQty = FAuxStockQty + " + dtDtl.Rows[i]["FQty"].ToString() + ",FAuxCommitQty = FAuxCommitQty +" + dtDtl.Rows[i]["FQty"].ToString() + " WHERE FInterID = " + dtDtl.Rows[i]["FInterID"].ToString() + " AND FEntryID = " + dtDtl.Rows[i]["FEntryID"].ToString() + @";
                    UPDATE SEOrderEntry SET FStockQty =  FStockQty + " + dtDtl.Rows[i]["FQty"].ToString() + ",FAuxStockQty = FAuxStockQty + " + dtDtl.Rows[i]["FQty"].ToString() + ",FAuxCommitQty = FAuxCommitQty +" + dtDtl.Rows[i]["FQty"].ToString() + " WHERE FInterID = " + dtDtl.Rows[i]["SEOrderInterID"].ToString() + " AND FEntryID = " + dtDtl.Rows[i]["SEOrderEntryID"].ToString() + ";";

                SqlOperation(0, strSQL);
            }
            #endregion

            if (conn.State == ConnectionState.Open)
                conn.Close();

            return "yes@ID:" + FInterID.ToString() + ";Number:" + FBillNo;
        }
        #endregion

        //-----Private Members
        #region Private
        /// <summary>
        /// 获取ICStockBillNo的最大内码和新的单据编码
        /// </summary>
        /// <param name="pFBillType">FTranType</param>
        /// <param name="pFInterID">最大内码</param>
        /// <param name="pFBillNo">新的单据编码</param>
        private static void GetICMaxIDAndBillNo(int pFBillType, out int pFInterID, out string pFBillNo)
        {
            SqlConnection conn = new SqlConnection(C_CONNECTIONSTRING);
            try
            {
                conn.Open();
                //内码
                SqlCommand cmd = new SqlCommand("GetICMaxNum", conn);

                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.Add("@TableName", SqlDbType.VarChar, 50);
                cmd.Parameters.Add("@FInterID", SqlDbType.Int);
                cmd.Parameters.Add("@Increment", SqlDbType.Int);
                cmd.Parameters.Add("@UserID", SqlDbType.Int);

                cmd.Parameters["@TableName"].Value = "ICStockBill";
                cmd.Parameters["@FInterID"].Direction = ParameterDirection.Output;//指定参数的方向为output(返回的值)
                cmd.Parameters["@Increment"].Value = 1;
                cmd.Parameters["@UserID"].Value = 16394;

                cmd.ExecuteNonQuery();//执行这个命令

                pFInterID = int.Parse(cmd.Parameters["@FInterID"].Value.ToString());

                //编号
                SqlCommand cmd2 = new SqlCommand("DM_GetICBillNo", conn);

                cmd2.CommandType = CommandType.StoredProcedure;
                cmd2.Parameters.Add("@FBillType", SqlDbType.Int);
                cmd2.Parameters.Add("@BillNo", SqlDbType.VarChar, 50);

                cmd2.Parameters["@FBillType"].Value = pFBillType;
                cmd2.Parameters["@BillNo"].Direction = ParameterDirection.Output;

                cmd2.ExecuteNonQuery();

                pFBillNo = cmd2.Parameters["@BillNo"].Value.ToString();
            }
            catch (Exception ex)
            {
                pFInterID = 0;
                pFBillNo = "Error:" + ex.Message;
            }
            finally
            {
                conn.Close();
            }
        }

        /// <summary>
        /// 判断DataTable中指定列是否包含指定个值
        /// </summary>
        /// <param name="pDataTable">DataTable</param>
        /// <param name="pCol">指定列</param>
        /// <param name="pValue">指定值</param>
        /// <returns></returns>
        private static bool ContainValue(DataTable pDataTable, string pCol, string pValue)
        {
            if (pDataTable == null || pDataTable.Rows.Count == 0 || !pDataTable.Columns.Contains(pCol))
                return false;

            for (int i = 0; i < pDataTable.Rows.Count; i++)
                if (pDataTable.Rows[i][pCol].ToString() == pValue)
                    return true;

            return false;
        }

        /// <summary>
        /// 对DataTable pCol列的前pIndex行求和
        /// </summary>
        /// <param name="pDataTable">DataTable</param>
        /// <param name="pCol">统计列</param>
        /// <param name="pIndex">序号</param>
        /// <returns></returns>
        private static decimal SumPre(DataTable pDataTable, string pCol, int pIndex)
        {
            if (pDataTable == null || pDataTable.Rows.Count == 0 || pIndex == 0 || !pDataTable.Columns.Contains(pCol))
                return 0;

            if (pIndex > pDataTable.Rows.Count - 1)
                pIndex = pDataTable.Rows.Count - 1;

            decimal dSum = 0;
            for (int i = 0; i < pIndex; i++)
                dSum += decimal.Parse(pDataTable.Rows[i][pCol].ToString());

            return dSum;
        }

        /// <summary>
        /// 获取物料的本次入库总数
        /// </summary>
        /// <param name="pDataTable">DataTable</param>
        /// <param name="pColMatch">匹配列</param>
        /// <param name="pValueMatch">匹配值</param>
        /// <param name="pColRetrun">返回第一行匹配成功的列值</param>
        /// <returns></returns>
        private static object GetValue(DataTable pDataTable, string pColMatch, string pValueMatch, string pColRetrun)
        {
            if (pDataTable == null || pDataTable.Rows.Count == 0 || !pDataTable.Columns.Contains(pColMatch) || !pDataTable.Columns.Contains(pColRetrun))
                return 0;

            for (int i = 0; i < pDataTable.Rows.Count; i++)
                if (pDataTable.Rows[i][pColMatch].ToString() == pValueMatch)
                    return pDataTable.Rows[i][pColRetrun];

            return null;
        }

        /// <summary>
        /// 更新DataTable
        /// </summary>
        /// <param name="pDataTable">DataTable</param>
        /// <param name="pColMatch">匹配列</param>
        /// <param name="pValueMatch">匹配值</param>
        /// <param name="pColSet">更新列</param>
        /// <param name="pValueSet">更新值</param>
        private static void UpdateTable(DataTable pDataTable, string pColMatch, string pValueMatch, string pColSet, decimal pValueSet)
        {
            if (pDataTable == null || pDataTable.Rows.Count == 0 || !pDataTable.Columns.Contains(pColMatch) || !pDataTable.Columns.Contains(pColSet))
                return;

            for (int i = 0; i < pDataTable.Rows.Count; i++)
                if (pDataTable.Rows[i][pColMatch].ToString() == pValueMatch)
                    pDataTable.Rows[i][pColSet] = pValueSet;
        }
        #endregion

        //-----SQL Helper
        #region 数据库操作
        /// <summary>
        /// 数据库操作
        /// </summary>
        /// <param name="pType">0、NonQuery;1、Scalar;2、Reader;3、DataTable;4、DataSet</param>
        /// <param name="pSQL">SQL Sentence</param>
        /// <returns></returns>
        private static object SqlOperation(int pType, string pSQL)
        {
            object obj;
            SqlDataAdapter adp;
            DataTable dt;
            DataSet ds;

            SqlConnection conn = new SqlConnection(C_CONNECTIONSTRING);

            try
            {
                conn.Open();
                SqlCommand cmd = conn.CreateCommand();
                cmd.CommandText = pSQL;

                switch (pType)
                {
                    case 0:
                        obj = cmd.ExecuteNonQuery();
                        break;
                    case 1:
                        obj = cmd.ExecuteScalar();
                        break;
                    case 2:
                        obj = cmd.ExecuteReader();
                        break;
                    case 3:
                        dt = new DataTable();
                        adp = new SqlDataAdapter(pSQL, conn);
                        adp.Fill(dt);
                        obj = dt;
                        break;
                    case 4:
                        ds = new DataSet();
                        adp = new SqlDataAdapter(pSQL, conn);
                        adp.Fill(ds);
                        obj = ds;
                        break;
                    default:
                        obj = null;
                        break;
                }
            }
            catch { return null; }
            finally
            {
                conn.Close();
            }

            return obj;
        }
        #endregion
    }
}