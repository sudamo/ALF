
SELECT * FROM T_TABLEDESCRIPTION WHERE FDESCRIPTION LIKE '%发货通知%'
SELECT * FROM t_FieldDescription where FTableID = 210009 AND FDescription LIKE '%项目%' order by ffieldname
SELECT * FROM ICBillNo WHERE fbillid = 81 or FBillName LIKE '%发货通知%'

select top 100 * from SEOutstock order by finterid desc

SELECT * FROM SEOutStock WHERE FBillNo = 'SEOUT18120215'

select top 1000 *
from [192.168.1.7].AIS20130728102030.dbo.ICStockBill
where ftrantype = 21
order by finterid desc

select * from [192.168.1.7].AIS20130728102030.dbo.DM_ICBillNo

select * from [192.168.1.7].AIS20190223120656.dbo.DM_ICBillNo

select * from [10.143.10.10].AIS20190305161000.dbo.DM_ICBillNo

--003|1|1|1|SHS216oc001s000075|WMS
--[010-00-000--M|XM001|01-01-01-1|1212|100|SHS216oc001s000075|WMS]

SELECT FInterID,FConsignee,FClosed FROM [192.168.1.7].AIS20130728102030.dbo.SEOrder WHERE FBillNo = 'SHS216oc001s000075'

SELECT FInterID,FConsignee,FClosed FROM SEOrder WHERE FBillNo = 'SHS216oc001s000075'

SELECT A.FInterID,AE.FEntryID,AE.FItemID,MTL.FUnitID,AE.FQty,AE.FStockQty,AE.FQty - AE.FStockQty CanOutQTY,AE.FPrice,MTL.FBatchManager 
FROM [10.143.10.10].AIS20190118090622.dbo.SEOrder A 
INNER JOIN [10.143.10.10].AIS20190118090622.dbo.SEOrderEntry AE ON A.FInterID = AE.FInterID 
INNER JOIN [10.143.10.10].AIS20190118090622.dbo.t_ICItem MTL ON AE.FItemID = MTL.FItemID 
WHERE A.FBillNo = 'SHS216oc001s000075' AND MTL.FNumber = '010-00-000--M'

SELECT FItemID FROM [10.143.10.10].AIS20190118090622.dbo.t_Stock WHERE FNumber = 'XM001'

SELECT FSPID FROM [10.143.10.10].AIS20190118090622.dbo.t_StockPlace WHERE FNumber = '01-01-01-1'

select top 1000 *
from [10.143.10.10].AIS20190118090622.dbo.ICStockBill
where fbillno like 'XOUT190301000%'


SELECT * FROM [10.143.10.10].AIS20190118090622.dbo.t_FieldDescription where FTableID = 210008 order by Ffieldname




update [10.143.10.10].AIS20190118090622.dbo.ICStockBill
set fyearperiod = '2019-03',fheadselfb0155 = 5590,fheadselfb0156=5367,fheadselfb0157=5452,fbrid = 0,frelatebrid = 0,
	fcheckerid = 16418,ffmanagerid = 5483,fsmanagerid =5483,fbillerid=16418,fdeptid = 9630,fempid = 5483,fsupplyid=5372,fposterid=16430,
	fcheckdate = '2019-03-01 00:00:00.000',fchildren = 1,forgbillinterid=0
where finterid = 2605

update [10.143.10.10].AIS20190118090622.dbo.ICStockBillentry set forderinterid = 1445,forderentryid = 1,forderbillno = 'SHS216oc001s000075' where finterid = 2605


select top 1 a.FSaleStyle,A.FSelTranType,a.FChildren,a.FYearPeriod,a.fdate,a.forgbillinterid
from ICStockBill a
inner join ICStockBillentry ae on a.finterid = ae.finterid

SELECT A.FInterID,AE.FEntryID,AE.FItemID,MTL.FUnitID,AE.FQty,AE.FStockQty,AE.FQty - AE.FStockQty CanOutQTY,AE.FPrice,MTL.FBatchManager
FROM SEOutStock A
INNER JOIN SEOutStockEntry AE ON A.FInterID = AE.FInterID
INNER JOIN t_ICItem MTL ON AE.FItemID = MTL.FItemID
WHERE A.FBillNo = 'SEOUT18120215' AND MTL.FNumber = '" + FItem + "'


--
SELECT FInterID,FClosed,FCustID FROM [10.143.10.10].AIS20190305161000.dbo.SEOutStock WHERE FBillNo = 'SHFH303oc001000319'

select *
from [10.143.10.10].AIS20190305161000.dbo.seorder a
inner join [10.143.10.10].AIS20190305161000.dbo.seorderentry ae on a.finterid = ae.finterid
where a.finterid = 1442

select *
from [10.143.10.10].AIS20190305161000.dbo.SEOutStock a
inner join [10.143.10.10].AIS20190305161000.dbo.SEOutStockentry ae on a.finterid = ae.finterid
where a.fbillno = 'SHFH303oc001000319'


SELECT A.FInterID,AE.FEntryID,AE.FItemID,MTL.FUnitID,AE.FQty,AE.FStockQty,AE.FQty - AE.FStockQty CanOutQTY,AE.FPrice,MTL.FBatchManager,ISNULL(B.FBillNo,'') SEOrderBillNo,ISNULL(B.FInterID,0) SEOrderInterID,ISNULL(BE.FEntryId,0) SEOrderEntryID,A.FCustID
FROM [10.143.10.10].AIS20190305161000.dbo.SEOutStock A
INNER JOIN [10.143.10.10].AIS20190305161000.dbo.SEOutStockEntry AE ON A.FInterID = AE.FInterID
INNER JOIN [10.143.10.10].AIS20190305161000.dbo.t_ICItem MTL ON AE.FItemID = MTL.FItemID
LEFT JOIN [10.143.10.10].AIS20190305161000.dbo.SEOrder B ON AE.FOrderBillNo = B.FBillNo
LEFT JOIN [10.143.10.10].AIS20190305161000.dbo.SEOrderEntry BE ON B.FInterID = BE.FInterID AND AE.FOrderEntryID = BE.FEntryID
WHERE A.FBillNo = 'SHFH216oc002000318'


select top 1000 *
from [10.143.10.10].AIS20190305161000.dbo.ICStockBill a
inner join [10.143.10.10].AIS20190305161000.dbo.ICStockBillentry ae on a.finterid = ae.finterid
where fbillno in('XOUT1903070002','SHOUT700oc001000186')

update [10.143.10.10].AIS20190305161000.dbo.ICStockBill set
	--FBrID=0,FOrgBillInterID=0
	--,FHeadSelfB0155=5590,FHeadSelfB0155=5367,FHeadSelfB0155=5452
	FSupplyID = 5371
where finterid = 2608

update [10.143.10.10].AIS20190305161000.dbo.ICStockBillentry set
	forderinterid =1442,forderentryid =1,forderbillno='SHS216oc002s000071',fseoutinterid=1383,fseoutentryid=1,fseoutbillno='SHFH216oc002000318',
	FEntrySelfB0174='N'
where finterid = 2608


select top 100 fobjectitem from icstockbill where fobjectitem <> 0
