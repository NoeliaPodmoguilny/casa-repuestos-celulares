# 📦 Casa Repuestos Celulares

Sistema de gestión para una casa de repuestos de celulares.
Permite administrar productos, usuarios, ventas y operaciones internas de forma organizada y eficiente.

---

## 🚀 Tecnologías utilizadas

* **C#**
* **.NET**
* **WinForms**
* **MySQL**
* **ADO.NET**
* **Arquitectura en capas**
* **Git**

---

## 🏗️ Arquitectura

El proyecto está organizado siguiendo una estructura por capas:

```
CasaRepuestos
│
├── Config        → Configuración general (connection string, entorno)
├── Forms         → Interfaz gráfica (WinForms)
├── Models        → Entidades del dominio
├── Services      → Lógica de negocio
└── Program.cs    → Punto de entrada
```

Separación clara entre UI y lógica para mantener mantenibilidad y escalabilidad.

---

## 🔐 Configuración del entorno

El proyecto utiliza **variables de entorno** para la cadena de conexión a la base de datos.

Variable requerida:

```
DB_CONNECTION_STRING
```
---

## 🗄️ Base de datos

Motor utilizado: **MySQL**

El sistema trabaja con tablas para:

* Usuarios
* Productos
* Ventas
* Roles
* Clientes

---

## ▶️ Cómo ejecutar el proyecto

1. Clonar el repositorio:

```bash
git clone https://github.com/NoeliaPodmoguilny/casa-repuestos-celulares.git
```

2. Configurar la variable de entorno `DB_CONNECTION_STRING`
3. Abrir la solución en Visual Studio
4. Ejecutar el proyecto

---

## 🧠 Funcionalidades principales

* Gestión de productos
* Control de stock
* Registro de ventas
* Gestión de compras y proveedores
* Reportes priodicos
* Apertura y cierre de caja
* Administración de usuarios
* Manejo de roles
* Interfaz de escritorio intuitiva

---

## 📌 Buenas prácticas aplicadas

* Separación de responsabilidades
* Uso de variables de entorno 
* Organización modular
* Código orientado a objetos
* Manejo estructurado de servicios

---

## 👩‍💻 Autora

**Noelia Podmoguilny**
Desarrolladora de Software
