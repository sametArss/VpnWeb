import axios from 'axios';
import AsyncStorage from '@react-native-async-storage/async-storage';

// DİKKAT: Bilgisayarının yerel IP adresini buraya yazmalısın. 
// Komut satırına 'ipconfig' yazarak IPv4 adresini bulabilirsin.
// Örnek: 192.168.1.15
const BASE_URL = 'http://172.20.10.6:5128/api'; // Android emülatör için varsayılan

const api = axios.create({
  baseURL: BASE_URL,
  headers: {
    'Content-Type': 'application/json',
  },
});

// Her istekte token'ı otomatik ekle
api.interceptors.request.use(async (config) => {
  const token = await AsyncStorage.getItem('token');
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }
  return config;
});

export default api;
