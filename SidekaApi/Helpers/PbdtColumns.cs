using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SidekaApi.Helpers
{
    public static class PbdtColumns
    {
        public static string[] getIdvColumns()
        {
            return new string[32] { "id", "kode_wilayah", "provinsi", "kabupaten", "kecamatan", "desa_kelurahan", "alamat", "nama", "nik", "nomor_urut_anggota_rumah_tangga", "hubungan_dengan_kepala_rumah_tangga", "nomor_urut_keluarga", "hubungan_dengan_kepala_keluarga", "jenis_kelamin", "umur", "status_perkawinan", "kepemilikan_buku_nikah_cerai", "tercantum_dalam_kk", "kepemilikan_kartu_identitas", "status_hamil", "jenis_cacat", "penyakit_kronis", "partisipasi_sekolah", "jenjang_pendidikan_tertinggi", "kelas_tertinggi", "ijazah_tertinggi", "bekerja_membantu_bekerja", "jumlah_jam_kerja", "lapangan_usaha", "status_kedudukan", "status_kesejahteraan", "nomor_urut_rumah_tangga"};
        }

        public static string[] getRtColumns()
        {
            return new string[79] { "id", "kode_wilayah", "provinsi", "kabupaten", "kecamatan", "desa_kelurahan", "alamat", "nama_krt", "jenis_kelamin", "umur", "jenjang_pendidikan_tertinggi", "lapangan_usaha", "status_kedudukan", "status_bangunan", "status_lahan", "luas_lantai", "jenis_lantai_terluas", "jenis_dinding_terluas", "kondisi_dinding", "jenis_atap_terluas", "kondisi_atap", "jumlah_kamar_tidur", "sumber_air_minum", "cara_memperoleh_air_minum", "sumber_penerangan_utama", "daya_listrik_terpasang", "bahan_bakar_untuk_memasak", "penggunaan_fasilitas_bab", "jenis_kloset", "tempat_pembuangan_akhir_tinja", "tabung_gas_5_5_kg_atau_lebih", "lemari_es", "ac", "pemanas_air", "telepon_rumah", "televisi", "emas_perhiasan_tabungan", "komputer_laptop", "sepeda", "sepeda_motor", "mobil", "perahu", "motor_tempel", "perahu_motor", "kapal", "jumlah_nomor_hp_aktif", "jumlah_tv_layar_datar_30_inch", "aset_lahan", "luas_lahan", "rumah_di_tempat_lain", "jumlah_sapi", "jumlah_kerbau", "jumlah_kuda", "jumlah_babi", "jumlah_kambing", "art_memiliki_usaha_sendiri", "kks_kps", "kip_bsm", "kis_bpjs", "bpjs_mandiri", "jamsostek", "asuransi", "pkh", "raskin", "kur", "nomor_urut_wus", "usia_kawin_suami_wus", "usia_kawin_istri_wus", "peserta_kb_wus", "metode_kontrasepsi_wus", "lama_kontrasepsi_tahun_wus", "lama_kontrasepsi_bulan_wus", "tempat_pelayanan_kb", "ingin_punya_anak_lagi", "alasan_tidak_kb_wus", "jumlah_anggota_rumah_tangga", "jumlah_keluarga", "status_kesejahteraan", "nomor_urut_rumah_tangga" };
        }
    }
}
