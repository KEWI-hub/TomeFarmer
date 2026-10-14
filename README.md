# TomeFarmer

ปลั๊กอิน Dalamud ที่สั่ง AutoDuty ให้วนลงดันเจี้ยนเพื่อฟาร์ม **Allagan Tomestone of Mathematics**

> ต้องติดตั้ง [AutoDuty](https://github.com/ffxivcode/AutoDuty) และ vnavmesh ไว้ก่อน
> ปลั๊กอิน automation แบบนี้ผิด ToS ของ FFXIV ใช้ด้วยความเสี่ยงของคุณเอง

## ติดตั้งผ่าน Custom Repository
1. ในเกมพิมพ์ `/xlsettings` แล้วไปที่แท็บ **Experimental**
2. ในส่วน *Custom Plugin Repositories* วาง URL นี้ ติ๊ก Enabled แล้วกด **Save**
   ```
   https://raw.githubusercontent.com/KEWI-hub/TomeFarmer/main/repo.json
   ```
3. พิมพ์ `/xlplugins` ค้นหา **TomeFarmer** แล้วกด Install

## ใช้งาน
- `/tomefarm` เปิด/ปิดหน้าต่าง
- `/tomefarm start`, `/tomefarm stop`

1. ตั้ง Duty Mode (Trust / Duty Support / Regular) และ rotation ในหน้าต่าง AutoDuty
2. เลือกดันใน TomeFarmer แล้วตั้ง Target count
3. กด **Start farming**

## วิธีทำงาน
ปลั๊กอินสั่ง AutoDuty ให้วนลงดันต่อเนื่อง แล้วจับช่วงเข้าและออกจากดันเพื่อวัดว่าแต่ละรอบได้ tomestone เท่าไรและใช้เวลานานแค่ไหน

ปลั๊กอินจะหยุดเองเมื่อเกิดข้อใดข้อหนึ่งต่อไปนี้
- จำนวน tomestone ถึงเป้า (สูงสุด 2000)
- รันครบจำนวนรอบที่ตั้งไว้
- รันติดกัน N รอบแล้วได้ +0

ตาราง **Duty stats** เก็บค่า tome ต่อรอบ เวลาที่อยู่ในดัน และ tome ต่อชั่วโมงของแต่ละดัน ใช้เทียบได้ว่าดันไหนฟาร์มคุ้มที่สุด

## ออกเวอร์ชันใหม่ (สำหรับผู้พัฒนา)
```powershell
.\release.ps1 1.0.1.0
```
สคริปต์จะ build อัปเดต `repo.json` push ขึ้น GitHub แล้วสร้าง GitHub Release ที่แนบ `latest.zip`
