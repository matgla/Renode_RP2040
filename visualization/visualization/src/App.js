import React from 'react';
import Breadboard from './components/Breadboard';
import Button from './components/Button';
import './App.css';

const App = () => {
  return (
    <div className='app'>
      <Breadboard gridRows={5} gridColumns={32} />
    </div>
  )
}

export default App;
